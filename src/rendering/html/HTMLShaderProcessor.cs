using Microsoft.Playwright;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Contains functions for processing shaders with HTML elements.
/// </summary>
public class HTMLShaderProcessor {
    private readonly IShaderProcessor _shaderProcessor;

    public HTMLShaderProcessor(IShaderProcessor shaderProcessor) {
        _shaderProcessor = shaderProcessor;
    }

    const string SHADER_KEY = "shader";
    const string SHADER_PARAMETERS_KEY = $"{SHADER_KEY}-params";
    const string SHADER_BG_KEY = "shader-bg";
    const string SHADER_BG_PARAMETERS_KEY  = $"{SHADER_BG_KEY}-params";
    const string IGNORE_PARENT_SHADERS_KEY = "ignoreParentShaders";
    const string SHADER_OUTPUT_CLASSNAME = "shader-output";

    private sealed class ElementInfo {
        public int Idx { get; set; }
        public string Id { get; set; } = "";
        public int Depth { get; set; }
    }

    /// <summary>
    /// Processes the elements of the page into different frames by increasing the time parameter of the shaders from 0 to 1
    ///
    /// </summary>
    /// <param name="page">html page</param>
    /// <returns>
    /// frames:
    ///     Array< (for each frame)
    ///         IReadOnlyList< (for each element)
    ///            Array<byte> (image data)
    ///        >
    ///     >,
    /// elements: IReadOnlyList<ILocator>,
    /// backgroundFrames?:
    ///     Array< (for each frame)
    ///         IReadOnlyList< (for each element)
    ///            Array<byte> (image data)
    ///        >
    ///     >,
    /// backgroundElements?: IReadOnlyList<ILocator>,
    /// </returns>
    public async Task<(
        IReadOnlyList<byte[]>[] frames,
        IReadOnlyList<ILocator> elements,
        IReadOnlyList<byte[]>[]? backgroundFrames,
        IReadOnlyList<ILocator>? backgroundElements
    )> ProcessShadersAsync(IPage page, int fps = 30, float duration = 1f) {
        int frameCount = _shaderProcessor.GetShaderFrameCount(fps, duration);
        
        var processedElements = new List<ILocator>();
        var processedBackgroundElements = new List<ILocator>();

        var processedFrames = new List<byte[]>[frameCount];
        var processedBackgroundFrames = new List<byte[]>[frameCount];

        for (int i = 0; i < frameCount; i++) {
            processedFrames[i] = new List<byte[]>();
            processedBackgroundFrames[i] = new List<byte[]>();
        }

        var shaderIds = await page.EvaluateAsync<ElementInfo[]>(
            """
            () => DocumentFunctions.getShadersDeepestFirst()
            """
        );
        Console.WriteLine($"Found {shaderIds.Length} {SHADER_KEY}/{SHADER_BG_KEY}/{IGNORE_PARENT_SHADERS_KEY} elements.");

        await CreateShaderLayerContainersAsync(page);

        // Cannot multithread because element processing order matters
        foreach (var idInfo in shaderIds) {
            string id = idInfo.Id;
            
            var element = page.Locator($"#{id}");
            var shader = await element.GetAttributeAsync(SHADER_KEY);
            var shaderBg = await element.GetAttributeAsync(SHADER_BG_KEY);
            Console.WriteLine($"Shaderizing element {idInfo.Idx + 1} of {shaderIds.Length} - shader property: {shader ?? "None"}, shader-bg property: {shaderBg ?? "None"}");

            if (!string.IsNullOrWhiteSpace(shaderBg)) {
                var box = await element.BoundingBoxAsync();

                if (box != null) {
                    int bgWidth = Math.Max(1, (int)Math.Ceiling(box.Width));
                    int bgHeight = Math.Max(1, (int)Math.Ceiling(box.Height));

                    var shader_params = await element.GetAttributeAsync(SHADER_BG_PARAMETERS_KEY);   
                    var frames = await _shaderProcessor.ApplyAnimatedToRectAsync(
                        page.Context, 
                        bgWidth, 
                        bgHeight, 
                        fps, 
                        duration,
                        ShaderInfo.FromShaderFileName(
                            shaderBg, 
                            ShaderParameters.ParseShaderParameters(shader_params)
                        )
                    );

                    for (int frameIdx = 0; frameIdx < frameCount; frameIdx++) {
                        processedBackgroundFrames[frameIdx].Add(frames[frameIdx]);
                    }

                    var backgroundLayer = await CreateShaderLayerAsync(element, frames[0], idInfo.Depth, background: true);
                    processedBackgroundElements.Add(backgroundLayer);
                }
            }

            if (!string.IsNullOrWhiteSpace(shader)) {
                var screenshot = await ScreenshotForShaderAsync(element);

                var shader_params = await element.GetAttributeAsync(SHADER_PARAMETERS_KEY);

                var frames = await _shaderProcessor.ApplyAnimatedAsync(
                    page.Context, 
                    screenshot,   
                    fps, 
                    duration,
                    ShaderInfo.FromShaderFileName(
                        shader, 
                        ShaderParameters.ParseShaderParameters(shader_params)
                    )
                );

                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++) {
                    processedFrames[frameIdx].Add(frames[frameIdx]);
                }

                var shaderLayer = await CreateShaderLayerAsync(element, frames[0], idInfo.Depth, background: false);
                processedElements.Add(shaderLayer);
            }

            if (!string.IsNullOrWhiteSpace(shader)) {
                await element.EvaluateAsync(
                    """
                    (element) => DocumentFunctions.hideOriginalElement(element)
                    """
                );
            }
        }

        IReadOnlyList<byte[]>[]? backgroundFrames = processedBackgroundElements.Count > 0 ? processedBackgroundFrames : null;
        IReadOnlyList<ILocator>? backgroundElements = processedBackgroundElements.Count > 0 ? processedBackgroundElements : null;

        return (processedFrames, processedElements, backgroundFrames, backgroundElements);
    }

    private static async Task CreateShaderLayerContainersAsync(IPage page) {
        await page.EvaluateAsync(
            """
            () => DocumentFunctions.createShaderLayerContainer()
            """
        );
    }

    private static async Task<ILocator> CreateShaderLayerAsync(ILocator element, byte[] image, int depth, bool background) {
        var base64 = Convert.ToBase64String(image);
        var dataUrl = $"data:image/png;base64,{base64}";
        var id = $"{SHADER_OUTPUT_CLASSNAME}-{Guid.NewGuid():N}";

        await element.EvaluateAsync(
            """
            (element, args) => DocumentFunctions.createShaderLayer(element, args)
            """,
            new { id, dataUrl, depth, background }
        );

        return element.Page.Locator($"#{id}");
    }

    private static async Task<byte[]> ScreenshotForShaderAsync(ILocator element) {
        // TODO, ignore siblings too
        var ignoredDescendants = await element.EvaluateAsync<string[]>(
            """
            (element) => DocumentFunctions.getDescendantsIgnoringParentShaders(element)
            """
        );
        await element.EvaluateAsync(
            """
            (element) => DocumentFunctions.setSiblingsVisible(element, false)
            """
        );
        if (ignoredDescendants.Length == 0) {
            try
            {
                return await element.ScreenshotAsync(new() {
                    Type = ScreenshotType.Png,
                    OmitBackground = true,
                });
            } finally {
                await element.EvaluateAsync(
                    """
                    (element) => DocumentFunctions.setSiblingsVisible(element, true)
                    """
                );
            }
            
        }

        /*
            Hide overlay layers sourced from ignored descendants, and hide the descendants themselves
            (covers ignoreParentShaders-only elements with no shader of their own, which don't get an overlay layer)
        */
        var hiddenCount = await element.Page.EvaluateAsync<int>(
            """
            (ids) => DocumentFunctions.setShaderLayersVisible(ids, false)
            """,
            ignoredDescendants
        );

        try {
            // OmitBackground makes the hidden descendant areas (and their shader layers) transparent, leaving holes where ignored descendants exist.
            return await element.ScreenshotAsync(new() {
                Type = ScreenshotType.Png,
                OmitBackground = true,
            });
        } finally {
            if (hiddenCount > 0) {
                await element.Page.EvaluateAsync(
                    """
                    (ids) => DocumentFunctions.setShaderLayersVisible(ids, true)
                    """,
                    ignoredDescendants
                );
            }
            await element.EvaluateAsync(
            """
            (element) => DocumentFunctions.setSiblingsVisible(element, true)
            """
        );
        }
    }
}