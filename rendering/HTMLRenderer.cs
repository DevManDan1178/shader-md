using Microsoft.Playwright;
using ShaderMarkdown.HTML;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Contains the method to apply shaders and export the readme
/// </summary>
public class HtmlRenderer {
    /// <summary>
    /// The waiting time before screenshotting, after adding all elements to the page at a frame.
    /// Gives time for the page to render everything.
    /// </summary>
    private const int PAGE_SCREENSHOT_WAIT_TIME = 5;

    private const string DOCUMENT_BACKGROUND_ID = "document-background";

    
    private readonly IShaderProcessor _shaderProcessor;
    private readonly HTMLShaderProcessor _htmlShaderProcessor;
    public HtmlRenderer(IShaderProcessor shaderProcessor) {
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
        string? backgroundShader = null,
        string? outerShader = null
    ) {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();

        var page = await browser.NewPageAsync(new() {
            ViewportSize = new() {
                Width = width,
                Height = height
            },
            DeviceScaleFactor = scale
        });

        await HTMLDocument.LoadPageDocumentFunctions(page);

        /* print browser logs on console?
        page.Console += (_, msg) =>
        {
            Console.WriteLine($"[Browser] {msg.Type}: {msg.Text}");
        };
        */

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
        Console.WriteLine($" Document size: {documentSize.Width}x{documentSize.Height}.");

        Console.WriteLine("Processing shaders.");
        var processed = await _htmlShaderProcessor.ProcessShadersAsync(page, fps, duration);
        
 
        byte[][]? documentBackgroundFrames = null;
        if (string.IsNullOrWhiteSpace(backgroundShader)) {
            Console.WriteLine("Setting page backgroun");
            await HTMLDocument.SetPageBackgroundAsync(page, backgroundColor);
        } else {
            Console.WriteLine("Generating page background frames");
            documentBackgroundFrames = await GetDocumentBackgroundFrames(page, documentSize, fps, duration, backgroundColor, backgroundShader);
        } 
        
        Console.WriteLine($"Now compositing.");
        
        byte[][] documentFrames = await GetDocumentFramesAsync(
            page, 
            processed, 
            documentBackgroundFrames
        );

        Console.WriteLine("Compositing frame finished.");

        if (outerShader != null) {
            Console.WriteLine($"Now applying outer shader to document: {outerShader}");
            documentFrames = await GetShaderizedDocumentFramesAsync(page, documentFrames, fps, outerShader);    
        }
        Console.WriteLine("Exporting.");
        await GifBuilder.SaveGifAsync(documentFrames, fps, outputPath);
    }

    private async Task<byte[][]> GetShaderizedDocumentFramesAsync(IPage page, byte[][] documentFrames, int fps, string shader) {
        byte[][] shaderizedDocumentFrames = new byte[documentFrames.Length][];
        float shaderTime = 0;
        await _shaderProcessor.LoadPageShaderScript(page);
        for (int i = 0; i < documentFrames.Length; shaderTime += (float) i++/fps) {
            Console.WriteLine($"Shaderizing: document frame {i + 1} of {documentFrames.Length}.");
            shaderizedDocumentFrames[i] = await _shaderProcessor.ApplyAsync(page, documentFrames[i], shader, new ShaderParameters {
                Time = shaderTime
            });
        }
        return shaderizedDocumentFrames;
    }

    private async Task<byte[][]> GetDocumentBackgroundFrames(
        IPage page, 
        DocumentSize documentSize, 
        int fps,
        float duration,
        string backgroundColor,
        string backgroundShader
    ) {
        byte[][] documentBackgroundFrames = await _shaderProcessor.ApplyAnimatedToRectAsync(
            page,
            documentSize.Width,
            documentSize.Height,
            backgroundShader,
            fps,
            duration,
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

        for (int frameIdx = 0; frameIdx < processedFrames.Length; ++frameIdx) {
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

        return documentFrames;
    }

   
}
