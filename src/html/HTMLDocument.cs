using Microsoft.Playwright;
using ShaderMarkdown.Files;

namespace ShaderMarkdown.HTML;

struct DocumentSize {
    public int Width;
    public int Height;
}

static class HTMLDocument {
    /// <summary>
    /// Replaces an image element's source with a base64-encoded PNG data URL.
    /// </summary>
    public static async Task SetElementImageAsync(ILocator image, byte[] data) {
        var base64 = Convert.ToBase64String(data);
        var dataUrl = $"data:image/png;base64,{base64}";

        await image.EvaluateAsync(
            """
            (image, dataUrl) => {
                image.src = dataUrl;
            }
            """,
            dataUrl);
    }

    /// <summary>
    /// Sets the background CSS value on both the html and body elements.
    /// </summary>
    public static async Task SetPageBackgroundAsync(IPage page, string background) {
        await page.EvaluateAsync(
            """
            (background) => {
                document.documentElement.style.background = background;
                document.body.style.background = background;
            }
            """,
            background
        );
    }

    /// <summary>
    /// Gets the full scrollable width and height of the document.
    /// </summary>
    /// <param name="page">page of the document</param>
    /// <returns>DocumentSize object with width and</returns>
    public static async Task<DocumentSize> GetDocumentSizeAsync(IPage page) {
        var size = await page.EvaluateAsync<int[]>(
            """
            () => {
                return [
                    document.documentElement.scrollWidth,
                    document.documentElement.scrollHeight
                ];
            }
            """
        );

        return new DocumentSize {
            Width = size[0],
            Height = size[1],
        };
    }

    /// <summary>
    /// Injects the DocumentFunctions script into the page, if not already loaded.
    /// </summary>
    public static async Task LoadPageDocumentFunctions(IPage page) {
        var alreadyLoaded = await page.EvaluateAsync<bool>(
            """
            () => typeof DocumentFunctions !== "undefined"
            """
        );

        if (!alreadyLoaded) {
            var documentFunctionsScript = await File.ReadAllTextAsync(WebScriptPaths.DOCUMENT_FUNCTIONS);

            await page.AddScriptTagAsync(new() {
                Content = documentFunctionsScript
            });
        }
    }

    /// <summary>
    /// Waits for the img element to fire decode() [when it finishes loading]
    /// </summary>
    /// <param name="locator">locator for the image</param>
    /// <returns>Task for waiting on decode()</returns>
    public static async Task WaitForImageDecodeAsync(ILocator locator) {
        try {
            await locator.EvaluateAsync(
                "async (el) => { if (el?.decode) { await el.decode(); } }"
            );
        } catch (Exception ex) {
            Console.WriteLine($"[!] Image decode failed or element unavailable: {ex.Message}");
        }
    }
    /// <summary>
    /// Waits two browser animation frames (after the next frame has been painted)
    /// </summary>
    /// <param name="page">Page</param>
    /// <returns>Task for waiting for two frames</returns>
    public static async Task WaitForNextPaintAsync(IPage page) {
        await page.EvaluateAsync(
            """
            () => new Promise(resolve => {
                requestAnimationFrame(() => requestAnimationFrame(resolve));
            })
            """
        );
    }
}