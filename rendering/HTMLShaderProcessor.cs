using System.Reflection.Metadata;
using Microsoft.Playwright;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Contains functions for processing shaders with HTML elements
/// </summary>
public class HTMLShaderProcessor {

    private readonly IShaderProcessor _shaderProcessor;
    public HTMLShaderProcessor(IShaderProcessor shaderProcessor) {
        _shaderProcessor = shaderProcessor;
    }

    const string SHADER_KEY = "shader";
    const string SHADER_BG_KEY = "shader-bg";
    
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
    /// </returns>
    public async Task<(
        IReadOnlyList<byte[]>[] frames,
        IReadOnlyList<ILocator> elements,
        IReadOnlyList<byte[]>[]? backgroundFrames,
        IReadOnlyList<ILocator>? backgroundElements
    )> ProcessShadersAsync(IPage page, int fps = 30, float duration = 1f) {
        var elements = page.Locator($"[{SHADER_KEY}]");
        var backgrounds = page.Locator($"[{SHADER_BG_KEY}]");
        int frameCount = (int)(fps * duration);

        List<ILocator> processedElements = new List<ILocator>();
        List<byte[]>[] processedFrames = new List<byte[]>[frameCount];

        for (int i = 0; i < frameCount; i++) {
            processedFrames[i] = new List<byte[]>();
        }

        // -----------------------
        // Regular shader elements
        // -----------------------

        int elementCount = await elements.CountAsync();
        for (int elementIdx = 0; elementIdx < elementCount; ++elementIdx) {
            Console.WriteLine("Shaderizing element " + (elementIdx + 1).ToString() + " of " + elementCount.ToString());

            var element = elements.Nth(elementIdx);
            var shader = await element.GetAttributeAsync(SHADER_KEY);

            if (string.IsNullOrWhiteSpace(shader)) {
                continue;
            }

            var screenshot = await element.ScreenshotAsync(new() {
                Type = ScreenshotType.Png,
                OmitBackground = true,
            });

            var frames = await _shaderProcessor.ApplyAnimatedAsync(
                page,
                screenshot,
                shader,
                fps,
                duration
            );

            for (int frameIdx = 0; frameIdx < frameCount; ++frameIdx) {
                processedFrames[frameIdx].Add(frames[frameIdx]);
            }

            var id = $"shader-source-{elementIdx}";

            await element.EvaluateAsync("(element, id) => element.id = id", id);

            processedElements.Add(page.Locator($"#{id}"));
        }

        // -------------------------
        // Background shader elements
        // -------------------------

        int backgroundCount = await backgrounds.CountAsync();

        if (backgroundCount == 0) {
            return (processedFrames, processedElements, null, null);
        }

        List<ILocator> processedBackgroundElements = new();
        List<byte[]>[] processedBackgroundFrames = new List<byte[]>[frameCount];

        for (int i = 0; i < frameCount; i++) {
            processedBackgroundFrames[i] = new List<byte[]>();
        }

        for (int bgIndex = 0; bgIndex < backgroundCount; ++bgIndex) {
            Console.WriteLine($"Shaderizing background {bgIndex + 1} of {backgroundCount}");

            var background = backgrounds.Nth(bgIndex);

            var shader = await background.GetAttributeAsync(SHADER_BG_KEY);

            if (string.IsNullOrWhiteSpace(shader)) {
                continue;
            }

            var box = await background.BoundingBoxAsync();

            if (box == null) {
                continue;
            }

            int width = Math.Max(1, (int)Math.Ceiling(box.Width));

            int height = Math.Max(1, (int)Math.Ceiling(box.Height));

            var frames = await _shaderProcessor.ApplyAnimatedToRectAsync(
                page,
                width,
                height,
                shader,
                fps,
                duration
            );

            for (int frameIdx = 0; frameIdx < frameCount; ++frameIdx) {
                processedBackgroundFrames[frameIdx].Add(frames[frameIdx]);
            }

            // Actual image that will sit behind
            var imageId = $"shader-bg-{bgIndex}";
            var createElementBackgroundScript = await File.ReadAllTextAsync(FilePaths.WebScriptPaths.CREATE_ELEMENT_BACKGROUND);

            await page.AddScriptTagAsync(new() {
                Content = createElementBackgroundScript
            });
            await background.EvaluateAsync<string>(
                """
                    (element, id) => {
                        CreateElementBackground.createElementBackground(element, id);
                    }
                """,
                imageId
            );

            processedBackgroundElements.Add(page.Locator($"#{imageId}"));
        }

        return (
            processedFrames,
            processedElements,
            processedBackgroundFrames,
            processedBackgroundElements
        );
    }
}