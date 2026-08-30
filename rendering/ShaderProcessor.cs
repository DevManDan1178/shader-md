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
        _rendererPath = FilePaths.WebScriptPaths.RENDER_SHADER;
    }

    public async Task<byte[]> ApplyAsync(IPage page, byte[] image, string shader, ShaderParameters shaderParameters) {
        var shaderPath = Path.Combine(_shaderDirectory, $"{shader}.frag");

        if (!File.Exists(shaderPath)) {
            throw new FileNotFoundException($"Shader '{shader}' was not found.", shaderPath);
        }

        var source = await File.ReadAllTextAsync(shaderPath);

        if (!File.Exists(_rendererPath)) {
            throw new FileNotFoundException("Shader renderer JavaScript was not found.", _rendererPath);
        }

        var rendererSource = await File.ReadAllTextAsync(_rendererPath);

        await page.AddScriptTagAsync(new() {
            Content = rendererSource
        });

        var imageBase64 = Convert.ToBase64String(image);

        var result = await page.EvaluateAsync<byte[]>(
            """
            async (args) => {
                return await RenderShader.renderShader(args);
            }
            """,
            new {
                imageBase64,
                fragmentSource = source,
                parameters = new {
                    time = (double) shaderParameters.Time,
                }
            });

        return result;
    }
}