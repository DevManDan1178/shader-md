using Microsoft.Playwright;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Implementation of the IShaderProcessor interface
/// Implements the application of a shader to an image
/// </summary>
public class ShaderProcessor : IShaderProcessor {
    private readonly string _shaderDirectory;
    private readonly string _rendererPath;

    public ShaderProcessor() {
        _shaderDirectory = FilePaths.Directories.DEFAULT_SHADER_DIRECTORY;
        _rendererPath = FilePaths.WebScriptPaths.SHADER_RENDERER;
    }

    public async Task LoadPageShaderScript(IPage page) {
        var rendererSource = await File.ReadAllTextAsync(_rendererPath);

        await page.AddScriptTagAsync(new() {
            Content = rendererSource
        });
    }

    public async Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo) {

        if (!File.Exists(shaderInfo.ShaderPath)) {
            Console.WriteLine(shaderInfo.ShaderPath);
            throw new FileNotFoundException($"Shader not found: {shaderInfo.ShaderPath}\nAre you sure \"{Path.GetFileName(shaderInfo.ShaderPath)}\" is the correct file name?");
        }

        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);

        if (!File.Exists(_rendererPath)) {
            throw new FileNotFoundException($"Shader renderer JavaScript not found: \"{_rendererPath}\".");
        }

        var imageBase64 = Convert.ToBase64String(image);

        var result = await page.EvaluateAsync<byte[]>(
            """
            async (args) => {
                return await ShaderRenderer.renderShader(args);
            }
            """,
            new {
                imageBase64,
                fragmentSource = source,
                parameters = new {
                    time = (double) shaderInfo.ShaderParameters.Time,
                    shaderProperties = shaderInfo.ShaderParameters.ShaderProperties,
                }
            });

        return result;
    }
}