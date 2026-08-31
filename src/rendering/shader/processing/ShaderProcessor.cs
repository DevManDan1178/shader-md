using System.Text.Json;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Implementation of the IShaderProcessor interface
/// Implements the application of a shader to an image
/// </summary>
public class ShaderProcessor : IShaderProcessor {
    private class RawFrameResult {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Pixels { get; set; } = [];
    }

    private readonly string _shaderDirectory;
    private readonly string _rendererPath;

    public ShaderProcessor() {
        _shaderDirectory = FilePaths.Directories.DEFAULT_SHADER_DIRECTORY;
        _rendererPath = FilePaths.WebScriptPaths.SHADER_RENDERER;
    }

    const string PAGE_SHADER_SCRIPT_LOADED_FLAG = "__shaderRendererLoaded";
    public async Task LoadPageShaderScript(IPage page) {
        var alreadyLoaded = await page.EvaluateAsync<bool>(
            $"() => !!window.{PAGE_SHADER_SCRIPT_LOADED_FLAG}"
        );

        if (!alreadyLoaded) {
            var rendererSource = await File.ReadAllTextAsync(_rendererPath);

            await page.AddScriptTagAsync(new() {
                Content = rendererSource
            });

            await page.EvaluateAsync("() => { window." + PAGE_SHADER_SCRIPT_LOADED_FLAG + " = true; }");
        }
    }

    public async Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float shaderTime) {

        if (!File.Exists(shaderInfo.ShaderPath)) {
            Console.WriteLine(shaderInfo.ShaderPath);
            throw new FileNotFoundException($"Shader not found: {shaderInfo.ShaderPath}\nAre you sure \"{Path.GetFileName(shaderInfo.ShaderPath)}\" is the correct file name?");
        }

        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);

        if (!File.Exists(_rendererPath)) {
            throw new FileNotFoundException($"Shader renderer JavaScript not found: \"{_rendererPath}\".");
        }

        var imageBase64 = Convert.ToBase64String(image);

        RawFrameResult frame = await page.EvaluateAsync<RawFrameResult>(
            """
            async (args) => {
                args.parameters.shaderProperties = JSON.parse(args.parameters.shaderProperties);

                return await ShaderRenderer.renderShaderRaw(args);
            }
            """,
            new {
                imageBase64,
                fragmentSource = source,
                shaderPath = shaderInfo.ShaderPath,
                parameters = new {
                    time = (double) shaderTime, // Breaks shaders when not casting to (double)
                    shaderProperties = JsonSerializer.Serialize(
                        shaderInfo.ShaderParameters.ShaderProperties
                    ),
                }
            }
        );

        // Encode RGBA bytes into image      
        using var img = Image.LoadPixelData<Rgba32>(frame.Pixels, frame.Width, frame.Height);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);

        return ms.ToArray();
    }
}