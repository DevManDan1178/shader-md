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

    public ShaderProcessor() {
    }

    const string PAGE_SHADER_RENDERER_LOADED_FLAG = "__shaderRendererLoaded";
    const string PAGE_STATIC_SHADER_RENDERER_LOADED_FLAG = "__staticShaderRendererLoaded";

    private async Task LoadPageScript(IPage page, string scriptPath, string scriptLoadedFlag) {
        var alreadyLoaded = await page.EvaluateAsync<bool>(
            $"() => !!window.{scriptLoadedFlag}"
        );

        if (!alreadyLoaded) {
            var rendererSource = await File.ReadAllTextAsync(scriptPath);

            await page.AddScriptTagAsync(new() {
                Content = rendererSource
            });

            await page.EvaluateAsync("() => { window." + scriptLoadedFlag + " = true; }");
        }
    }
    public async Task LoadPageShaderRenderer(IPage page) {
        await LoadPageScript(page, FilePaths.WebScriptPaths.SHADER_RENDERER, PAGE_SHADER_RENDERER_LOADED_FLAG);
    }

    public async Task LoadPageStaticShaderRenderer(IPage page) {
         await LoadPageScript(page, FilePaths.WebScriptPaths.STATIC_SHADER_RENDERER, PAGE_STATIC_SHADER_RENDERER_LOADED_FLAG);
    }

    public async Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float shaderTime) {
        if (shaderInfo.ShaderPath.Trim() == "") {
            return image;
        }
        if (! await page.EvaluateAsync<bool>(
            $"() => !!window.{PAGE_SHADER_RENDERER_LOADED_FLAG}"
        )) {
            throw new Exception("Shader renderer not loaded"); 
        }
        if (!File.Exists(shaderInfo.ShaderPath)) {
            Console.WriteLine(shaderInfo.ShaderPath);
            throw new FileNotFoundException($"Shader not found: {shaderInfo.ShaderPath}\nAre you sure \"{Path.GetFileName(shaderInfo.ShaderPath)}\" is the correct file name?");
        }

        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);

        if (!File.Exists(FilePaths.WebScriptPaths.SHADER_RENDERER)) {
            throw new FileNotFoundException($"Shader renderer JavaScript not found: \"{FilePaths.WebScriptPaths.SHADER_RENDERER}\".");
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

    public async Task<byte[][]> ApplyStaticBatchAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float[] shaderTimes) {
        if (shaderInfo.ShaderPath.Trim() == "") {
            byte[][] frames = new byte[shaderTimes.Length][];
            for (int i = 0; i < frames.Length; ++i) {
                frames[i] = image;
            }
            return frames;
        }
        if (! await page.EvaluateAsync<bool>(
            $"() => !!window.{PAGE_STATIC_SHADER_RENDERER_LOADED_FLAG}"
        )) {
            throw new Exception("Static shader renderer not loaded"); 
        }
        if (shaderTimes.Length == 0) {
            return [];
        }

        if (!File.Exists(shaderInfo.ShaderPath)) {
            Console.WriteLine(shaderInfo.ShaderPath);
            throw new FileNotFoundException($"Shader not found: {shaderInfo.ShaderPath}\nAre you sure \"{Path.GetFileName(shaderInfo.ShaderPath)}\" is the correct file name?");
        }

        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);

        if (!File.Exists(FilePaths.WebScriptPaths.STATIC_SHADER_RENDERER)) {
            throw new FileNotFoundException($"Static shader renderer JavaScript not found: \"{FilePaths.WebScriptPaths.STATIC_SHADER_RENDERER}\".");
        }

        var imageBase64 = Convert.ToBase64String(image);

        // Same shaderProperties for every frame in this batch - serialize once.
        var shaderPropertiesJson = JsonSerializer.Serialize(shaderInfo.ShaderParameters.ShaderProperties);

        var frameArgs = shaderTimes
            .Select(time => new {
                time = (double) time, // Breaks shaders when not casting to (double)
                shaderProperties = shaderPropertiesJson
            })
            .ToArray();

        RawFrameResult[] results = await page.EvaluateAsync<RawFrameResult[]>(
            """
            async (args) => {
                for (const frame of args.frames) {
                    frame.shaderProperties = JSON.parse(frame.shaderProperties);
                }

                return await StaticShaderRenderer.renderStaticShaderBatchRaw(args);
            }
            """,
            new {
                imageBase64,
                fragmentSource = source,
                shaderPath = shaderInfo.ShaderPath,
                frames = frameArgs
            }
        );

        var output = new byte[results.Length][];

        for (int i = 0; i < results.Length; i++) {
            using var img = Image.LoadPixelData<Rgba32>(results[i].Pixels, results[i].Width, results[i].Height);
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            output[i] = ms.ToArray();
        }

        return output;
    }
}