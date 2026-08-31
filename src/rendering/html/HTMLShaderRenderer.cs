using Microsoft.Playwright;
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
        string outputPath,
        int width = 1200,
        int height = 800,
        int fps = 30,
        float duration = 1f,
        float scale = 2,
        string backgroundColor = "#0d1117",
        ShaderInfo? backgroundShaderInfo = null,
        ShaderInfo? outerShaderInfo = null
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

        await _shaderProcessor.LoadPageShaderScript(page);
        
        await HTMLDocument.LoadPageDocumentFunctions(page);

        page.Console += (_, msg) => {
            if (msg.Text.Contains("GPU stall due to ReadPixels")) {  
                // Unavoidable warning       
                return;
            }
            Console.WriteLine($"[Browser] {msg.Type}: {msg.Text}");
        };

        var fullHtml = await page.EvaluateAsync<string>(
            """
                html => DocumentFunctions.createDocument(html)
            """,
            html
        );
        
        await page.SetContentAsync(fullHtml);
    
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.EvaluateAsync("() => document.fonts.ready");
        
        DocumentSize documentSize = await HTMLDocument.GetDocumentSizeAsync(page);
        Console.WriteLine($"Shaderizing document. Size: {documentSize.Width}x{documentSize.Height}.");

        Console.WriteLine("Processing shaders.");
        var processed = await _htmlShaderProcessor.ProcessShadersAsync(page, fps, duration);
        
 
        byte[][]? documentBackgroundFrames = null;
        if (backgroundShaderInfo == null) {
            Console.WriteLine("Setting page background");
            await HTMLDocument.SetPageBackgroundAsync(page, backgroundColor);
        } else {
            Console.WriteLine("Shaderizing page background");
            documentBackgroundFrames = await GetDocumentBackgroundFrames(page, documentSize, fps, duration, backgroundColor, backgroundShaderInfo);
        } 
        
        Console.WriteLine($"Now compositing.");
        
        byte[][] documentFrames = await GetDocumentFramesAsync(
            page, 
            processed, 
            documentBackgroundFrames
        );

        if (outerShaderInfo != null) {
            Console.WriteLine($"Applying outer shader to document: {outerShaderInfo.ShaderPath}");
            documentFrames = await _shaderProcessor.ApplyOverAnimatedAsync(page.Context, documentFrames, fps, outerShaderInfo);
        }
        Console.WriteLine("Exporting.");
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


    const int COMPOSITING_THREADS_COUNT = 4;
    private async Task<byte[][]> GetDocumentFramesAsync(
        IPage page,
        (
            IReadOnlyList<byte[]>[] frames, 
            IReadOnlyList<ILocator> elements, 
            IReadOnlyList<byte[]>[]? backgroundFrames, 
            IReadOnlyList<ILocator>? backgroundElements
        ) processed,
        byte[][]? documentBackgroundFrames
    ) {
        
        IReadOnlyList<byte[]>[] processedFrames = processed.frames;
        IReadOnlyList<ILocator> processedElements = processed.elements;

        IReadOnlyList<byte[]>[]? processedBackgroundFrames = processed.backgroundFrames;
        IReadOnlyList<ILocator>? processedBackgroundElements = processed.backgroundElements;

        byte[][] documentFrames = new byte[processedFrames.Length][];

        // No race conditions since every thread writes to different frames independently
        await Task.WhenAll(
            Enumerable.Range(0, COMPOSITING_THREADS_COUNT)
                .Select(async (workerIdx) => {
                    for (int frameIdx = 0 + workerIdx; frameIdx < processedFrames.Length; frameIdx += COMPOSITING_THREADS_COUNT) {
                        Console.WriteLine("Compositing frame " + (frameIdx + 1).ToString() + " of " + processedFrames.Length + " total");

                        // ---------------
                        // Regular Shaders
                        // ---------------

                        IReadOnlyList<byte[]> frameElements = processedFrames[frameIdx];

                        for (int elementIdx = 0; elementIdx < frameElements.Count; ++elementIdx) {
                            await HTMLDocument.SetElementImageAsync(
                                processedElements[elementIdx],
                                frameElements[elementIdx]
                            );
                        }

                        // --------------------------
                        // Element background Shaders
                        // --------------------------

                        if (processedBackgroundFrames != null && processedBackgroundElements != null) {
                            var frameBackgrounds = processedBackgroundFrames[frameIdx];

                            for (int bgIdx = 0; bgIdx < frameBackgrounds.Count; ++bgIdx) {
                                await HTMLDocument.SetElementImageAsync(
                                    processedBackgroundElements[bgIdx],
                                    frameBackgrounds[bgIdx]);
                            }
                        }

                        // ------------------
                        // Background Shaders
                        // ------------------

                        if (documentBackgroundFrames != null) {
                            var backgroundImage = page.Locator($"#{DOCUMENT_BACKGROUND_ID}");

                            await HTMLDocument.SetElementImageAsync(
                                backgroundImage,
                                documentBackgroundFrames[frameIdx]
                            );
                        }


                        // -----------------
                        // Apply to the page
                        // -----------------

                        await page.WaitForTimeoutAsync(PAGE_SCREENSHOT_WAIT_TIME);

                        documentFrames[frameIdx] = await page.ScreenshotAsync(new() {
                            FullPage = true,
                        });
                    }
                }
            )
        );
        return documentFrames;
    }

   
}
