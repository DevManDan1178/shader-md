using System.Text.Json;
using Microsoft.Playwright;
using ShaderMarkdown.Config;
using ShaderMarkdown.HTML;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Contains the method to apply shaders and export the readme
/// </summary>
public class HtmlShaderRenderer {
    /// <summary>
    /// The waiting time before screenshotting, after adding all elements to the page at a frame.
    /// Gives time for the page to render everything.
    /// </summary>
    private const int PAGE_SCREENSHOT_WAIT_TIME = 5;

    private const string DOCUMENT_BACKGROUND_ID = "document-background";

    
    private readonly IShaderProcessor _shaderProcessor;
    private readonly HTMLShaderProcessor _htmlShaderProcessor;
    public HtmlShaderRenderer(IShaderProcessor shaderProcessor) {
        _shaderProcessor = shaderProcessor;
        _htmlShaderProcessor = new (_shaderProcessor);
    }

    public async Task RenderAsync(
        string html,
        ShaderConfig shaderConfig,
        string outputPath,
        int width = 1200,
        int height = 800,
        int fps = 30,
        float duration = 1f,
        float scale = 1f,
        string backgroundColor = "#0d1117",
        bool reverseLoopFromEnd = false
    ) {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();

        var browserContext = await browser.NewContextAsync(new() {
            ViewportSize = new() {
                Width = width,
                Height = height
            },
            DeviceScaleFactor = scale
        });

        var page = await browserContext.NewPageAsync();
        page.Console += (_, msg) => {
            if (msg.Text.Contains("GPU stall due to ReadPixels")) {  
                // Unavoidable warning       
                return;
            }
            Console.WriteLine($"[Browser] {msg.Type}: {msg.Text}");
        };
        await _shaderProcessor.LoadPageShaderRenderer(page);
        await _shaderProcessor.LoadPageStaticShaderRenderer(page);
        
        await HTMLDocument.LoadPageDocumentFunctions(page);

        var fullHtml = await page.EvaluateAsync<string>(
            """
                (args) => {
                const pageShaderParameters = JSON.parse(args.pageShaderParameters);
                    return DocumentFunctions.createShaderizedDocument(args.pageHtml, pageShaderParameters)
                }
            """,
            new {
                pageHtml = html,
                pageShaderParameters = JsonSerializer.Serialize(shaderConfig.DefaultPageElementShaders),
            }
        );
        
        await page.SetContentAsync(fullHtml);
    
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.EvaluateAsync("() => document.fonts.ready");
        
        DocumentSize documentSize = await HTMLDocument.GetDocumentSizeAsync(page);
        Console.WriteLine($"Shaderizing document. Size: {documentSize.Width}x{documentSize.Height}.");

        Console.WriteLine("Processing shaders.");
        var processed = await _htmlShaderProcessor.ProcessShadersAsync(page, shaderConfig.ShadersRootDirectory, fps, duration);
        
        byte[][]? documentBackgroundFrames = null;
        SerializableShaderInfo? backgroundShader = shaderConfig.DocumentShaders.Background;
        if (backgroundShader == null) {
            Console.WriteLine("Setting page background");
            await HTMLDocument.SetPageBackgroundAsync(page, backgroundColor);
        } else {
            Console.WriteLine("Shaderizing page background");
            documentBackgroundFrames = await GetDocumentBackgroundFrames(page, documentSize, fps, duration, backgroundColor, backgroundShader.ToShaderInfo(shaderConfig.ShadersRootDirectory));
        } 
        
        Console.WriteLine($"Now compositing.");
        string currentDocumentHtml = await page.ContentAsync();
        byte[][] documentFrames = await GetDocumentFramesAsync(
            page, 
            currentDocumentHtml,
            processed, 
            documentBackgroundFrames,
            page.Context
        );

        SerializableShaderInfo? finalizeShaderInfo = shaderConfig.DocumentShaders.Finalize;
        if (finalizeShaderInfo != null) {
            Console.WriteLine($"Applying finalize shader to document: \"{finalizeShaderInfo.ShaderPath}\".");
            documentFrames = await _shaderProcessor.ApplyOverAnimatedAsync(page.Context, documentFrames, fps, finalizeShaderInfo.ToShaderInfo(shaderConfig.ShadersRootDirectory));
        }
        Console.WriteLine("Exporting.");
        if (reverseLoopFromEnd && documentFrames.Length > 2) {
            // Duplicates every frame EXCEPT last one and first one for the loop
            byte[][] loopedDocumentFrames = new byte[(documentFrames.Length - 1) * 2][];
            for (int i = 0; i < documentFrames.Length; ++i) {
                loopedDocumentFrames[i] = documentFrames[i];
                if (i > 0 && i < documentFrames.Length - 1) {
                    int secondFrameIdx = loopedDocumentFrames.Length - i;
                    loopedDocumentFrames[secondFrameIdx] = documentFrames[i];
                }
            }
            await GifBuilder.SaveGifAsync(loopedDocumentFrames, fps, outputPath);
            return;
        }
        await GifBuilder.SaveGifAsync(documentFrames, fps, outputPath);
    }


    private async Task<byte[][]> GetDocumentBackgroundFrames(
        IPage page, 
        DocumentSize documentSize, 
        int fps,
        float duration,
        string backgroundColor,
        ShaderInfo backgroundShaderInfo
    ) {
        byte[][] documentBackgroundFrames = await _shaderProcessor.ApplyAnimatedToRectAsync(
            page.Context,
            documentSize.Width,
            documentSize.Height,
            fps,
            duration,
            backgroundShaderInfo,
            backgroundColor
        );

        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(documentBackgroundFrames[0])}";

        await page.EvaluateAsync<string>(
            """
                ({ dataUrl, id, width, height }) => {
                    return DocumentFunctions.createDocumentBackground(
                        dataUrl,
                        id,
                        width,
                        height
                    );
                }
            """,
            new {
                dataUrl,
                id = DOCUMENT_BACKGROUND_ID,
                width = documentSize.Width,
                height = documentSize.Height
            }
        );
        return documentBackgroundFrames;
    }


    private async Task<byte[][]> GetDocumentFramesAsync(
        IPage page,
        string documentHtml,
        (
            IReadOnlyList<byte[]>[] frames, 
            IReadOnlyList<ILocator> elements, 
            IReadOnlyList<byte[]>[]? backgroundFrames, 
            IReadOnlyList<ILocator>? backgroundElements
        ) processed,
        byte[][]? documentBackgroundFrames,
        IBrowserContext browserContext
    ) {
        const int MAXIMUM_WORKER_COUNT = 10;
        const int MINIMUM_FRAMES_PER_WORKER = 3;
        IReadOnlyList<byte[]>[] processedFrames = processed.frames;
        IReadOnlyList<ILocator> processedElements = processed.elements;
        IReadOnlyList<byte[]>[]? processedBackgroundFrames = processed.backgroundFrames;
        IReadOnlyList<ILocator>? processedBackgroundElements = processed.backgroundElements;

        byte[][] documentFrames = new byte[processedFrames.Length][];

        async Task renderFrameOnPage(IPage workerPage, int frameIdx) {
            IReadOnlyList<byte[]> frameElements = processedFrames[frameIdx];
            for (int elementIdx = 0; elementIdx < frameElements.Count; ++elementIdx) {
                await HTMLDocument.SetElementImageAsync(
                    workerPage.Locator($"#{await processedElements[elementIdx].GetAttributeAsync("id")}"),
                    frameElements[elementIdx]
                );
            }

            if (processedBackgroundFrames != null && processedBackgroundElements != null) {
                var frameBackgrounds = processedBackgroundFrames[frameIdx];
                for (int bgIdx = 0; bgIdx < frameBackgrounds.Count; ++bgIdx) {
                    await HTMLDocument.SetElementImageAsync(
                        workerPage.Locator($"#{await processedBackgroundElements[bgIdx].GetAttributeAsync("id")}"),
                        frameBackgrounds[bgIdx]
                    );
                }
            }

            if (documentBackgroundFrames != null) {
                var backgroundImage = workerPage.Locator($"#{DOCUMENT_BACKGROUND_ID}");
                await HTMLDocument.SetElementImageAsync(backgroundImage, documentBackgroundFrames[frameIdx]);
            }

            await workerPage.WaitForTimeoutAsync(PAGE_SCREENSHOT_WAIT_TIME);

            documentFrames[frameIdx] = await workerPage.ScreenshotAsync(new() { FullPage = true });
        }

        int workerCount = Math.Max(1, Math.Min(MAXIMUM_WORKER_COUNT, processedFrames.Length / MINIMUM_FRAMES_PER_WORKER));

        int processedFramesCounter = 0;
        await Task.WhenAll(
            Enumerable.Range(0, workerCount)
                .Select(async workerIdx => {
                    IPage workerPage = workerIdx == 0 ? page : await browserContext.NewPageAsync();

                    if (workerIdx != 0) {
                        await workerPage.SetContentAsync(documentHtml);
                        await workerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        await workerPage.EvaluateAsync("() => document.fonts.ready");
                    }

                    for (int frameIdx = workerIdx; frameIdx < processedFrames.Length; frameIdx += workerCount) {
                        int processedCount = Interlocked.Increment(ref processedFramesCounter);
                        Console.WriteLine($"Compositing frame: {processedCount}/{processedFrames.Length}");

                        await renderFrameOnPage(workerPage, frameIdx);
                    }

                    if (workerIdx != 0) {
                        await workerPage.CloseAsync();
                    }
                })
        );

        return documentFrames;
    }
}
