using System.Diagnostics.Contracts;
using System.Text.Json;
using Microsoft.Playwright;
using ShaderMarkdown.Config;
using ShaderMarkdown.HTML;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Contains the method to apply shaders and export the readme
/// </summary>
public class HtmlShaderRenderer {
    private const string DOCUMENT_BACKGROUND_ID = "document-background";

    
    private readonly IShaderProcessor _shaderProcessor;
    private readonly HTMLShaderProcessor _htmlShaderProcessor;
    public HtmlShaderRenderer(IShaderProcessor shaderProcessor) {
        _shaderProcessor = shaderProcessor;
        _htmlShaderProcessor = new (_shaderProcessor);
    }

    [Pure]
    public async Task<byte[][]> GetShaderizedHTMLAsync(
        string html,
        ShaderConfig shaderConfig,
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
        await page.EvaluateAsync("() => DocumentFunctions.waitForImagesSettled()");
        
        DocumentSize documentSize = await HTMLDocument.GetDocumentSizeAsync(page);
        Console.WriteLine($"Shaderizing document. Size: {documentSize.Width}x{documentSize.Height}.");

        Console.WriteLine("Processing shaders.");
        var processed = await _htmlShaderProcessor.ProcessShadersAsync(page, fps, duration, shaderConfig.ShadersRootDirectory, backgroundColor);
        
        byte[][]? documentBackgroundFrames = null;
        SerializableShaderInfo backgroundShaderInfo = shaderConfig.DocumentShaders.Background;
        if (backgroundShaderInfo.IsValid()) {
            Console.WriteLine($"Shaderizing page background color: {backgroundColor}.");
            documentBackgroundFrames = await GetDocumentBackgroundFrames(page, documentSize, fps, duration, backgroundColor, backgroundShaderInfo.ToShaderInfo(shaderConfig.ShadersRootDirectory));
        } else {
            Console.WriteLine($"Setting page background color: {backgroundColor}.");
            await HTMLDocument.SetPageBackgroundAsync(page, backgroundColor);
        } 
        
        Console.WriteLine($"Now compositing.");
        string currentDocumentHtml = await page.ContentAsync();
        byte[][] documentFrames = await CompositeDocumentFramesAsync(
            page, 
            processed, 
            documentBackgroundFrames,
            documentSize
        );

        SerializableShaderInfo finalizeShaderInfo = shaderConfig.DocumentShaders.Finalize;
        if (finalizeShaderInfo.IsValid()) {
            Console.WriteLine($"Applying finalize shader to document: \"{finalizeShaderInfo.ShaderPath}\".");
            documentFrames = await _shaderProcessor.ApplyOverAnimatedAsync(page.Context, documentFrames, fps, finalizeShaderInfo.ToShaderInfo(shaderConfig.ShadersRootDirectory));
        }

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
            return loopedDocumentFrames;
        }
        return documentFrames;
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


    private async Task<byte[][]> CompositeDocumentFramesAsync(
        IPage page,
        (
            IReadOnlyList<byte[]>[] frames, 
            IReadOnlyList<ILocator> elements, 
            IReadOnlyList<byte[]>[]? backgroundFrames, 
            IReadOnlyList<ILocator>? backgroundElements
        ) processed,
        byte[][]? documentBackgroundFrames,
        DocumentSize documentSize
    ) {
        IReadOnlyList<byte[]>[] processedFrames = processed.frames;
        IReadOnlyList<ILocator> processedElements = processed.elements;
        IReadOnlyList<byte[]>[]? processedBackgroundFrames = processed.backgroundFrames;
        IReadOnlyList<ILocator>? processedBackgroundElements = processed.backgroundElements;

        byte[][] documentFrames = new byte[processedFrames.Length][];

        await page.SetViewportSizeAsync(documentSize.Width, documentSize.Height);
        await page.EvaluateAsync("() => DocumentFunctions.resyncShaderLayerPositions()");
        
        for (int frameIdx = 0; frameIdx < processedFrames.Length; ++frameIdx) {
            Console.WriteLine($"Compositing frame: {frameIdx + 1}/{processedFrames.Length}");

            IReadOnlyList<byte[]> frameElements = processedFrames[frameIdx];
            for (int elementIdx = 0; elementIdx < frameElements.Count; ++elementIdx) {
                await HTMLDocument.SetElementImageAsync(processedElements[elementIdx], frameElements[elementIdx]);
                await WaitForImageDecodeAsync(processedElements[elementIdx]);
            }

            if (processedBackgroundFrames != null && processedBackgroundElements != null) {
                var frameBackgrounds = processedBackgroundFrames[frameIdx];
                for (int bgIdx = 0; bgIdx < frameBackgrounds.Count; ++bgIdx) {
                    await HTMLDocument.SetElementImageAsync(processedBackgroundElements[bgIdx], frameBackgrounds[bgIdx]);
                    await WaitForImageDecodeAsync(processedBackgroundElements[bgIdx]);
                }
            }

            if (documentBackgroundFrames != null) {
                var backgroundImage = page.Locator($"#{DOCUMENT_BACKGROUND_ID}");
                await HTMLDocument.SetElementImageAsync(backgroundImage, documentBackgroundFrames[frameIdx]);
                await WaitForImageDecodeAsync(backgroundImage);
            }

            await page.EvaluateAsync("() => window.scrollTo(0, 0)");
            await HTMLDocument.WaitForNextPaintAsync(page);

            documentFrames[frameIdx] = await page.ScreenshotAsync();
        }

        return documentFrames;
    }

    private static async Task WaitForImageDecodeAsync(ILocator locator) {
        try {
            await locator.EvaluateAsync(
                "async (el) => { if (el?.decode) { await el.decode(); } }"
            );
        } catch (Exception ex) {
            Console.WriteLine($"[!] Image decode failed or element unavailable: {ex.Message}");
        }
    }
}
