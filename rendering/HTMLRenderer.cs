using Microsoft.Playwright;

namespace ShaderMarkdown.Rendering;

public class HtmlRenderer
{
    private readonly IShaderProcessor _shaderProcessor;

    public HtmlRenderer(IShaderProcessor shaderProcessor) {
        _shaderProcessor = shaderProcessor;
    }

    public async Task RenderAsync(string html, string outputPath, int width = 1200, int height = 800, float scale = 2) {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();

        var page = await browser.NewPageAsync(new() {
            ViewportSize = new() {
                Width = width,
                Height = height
            },
            DeviceScaleFactor = scale
        });

        var fullHtml =
            """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">

                <style>
                    body {
                        margin: 0;
                        padding: 40px;
                        background: #0d1117;
                        color: #c9d1d9;
                        font-family: Arial, sans-serif;
                    }

                    h1 {
                        color: #58a6ff;
                    }

                    h2 {
                        color: #79c0ff;
                    }

                    code {
                        background: #161b22;
                        padding: 2px 5px;
                        border-radius: 4px;
                    }

                    pre {
                        background: #161b22;
                        padding: 16px;
                        border-radius: 8px;
                    }

                    img {
                        max-width: 100%;
                    }
                </style>
            </head>

            <body>
            """ +
            html +
            """
            </body>
            </html>
            """;

        await page.SetContentAsync(fullHtml);

        // Wait for the document to finish loading.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for fonts to finish loading.
        await page.EvaluateAsync("() => document.fonts.ready");

        // Find and process every element that has a shader attribute.
        await ProcessShadersAsync(page);

        // Render the final document.
        await page.ScreenshotAsync(new() {
            Path = outputPath,
            FullPage = true
        });
    }

    private async Task ProcessShadersAsync(IPage page) {
        var elements = page.Locator("[shader]");

        while (await elements.CountAsync() > 0)
        {
            var element = elements.First;
            var shader = await element.GetAttributeAsync("shader");

            if (string.IsNullOrWhiteSpace(shader)) {
                continue;
            }

            Console.WriteLine($"Processing shader: {shader}");

            var screenshot = await element.ScreenshotAsync();
            var result = await _shaderProcessor.ApplyAsync(page, screenshot, shader);

            await ReplaceElementAsync(element, result);

            Console.WriteLine("Element replaced.");
        }
    }

    private static async Task ReplaceElementAsync(ILocator element, byte[] image) {
        var base64 = Convert.ToBase64String(image);
        var dataUrl = $"data:image/png;base64,{base64}";

        await element.EvaluateAsync(
            """
            (element, dataUrl) => {
                const rect = element.getBoundingClientRect();
                const computed = getComputedStyle(element);
                const image = document.createElement("img");

                image.src = dataUrl;
                image.style.width = rect.width + "px";
                image.style.height = rect.height + "px";
                image.style.display = computed.display;
                image.style.verticalAlign = computed.verticalAlign;
                image.style.objectFit = "fill";

                element.replaceWith(image);
            }
            """,
            dataUrl);
    }
}