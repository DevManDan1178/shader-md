using Microsoft.Playwright;

namespace ShaderMarkdown.Rendering;

public class ShaderProcessor : IShaderProcessor
{
    private readonly string _shaderDirectory;
    private readonly string _rendererPath;

    public ShaderProcessor()
    {
        _shaderDirectory = Path.Combine(AppContext.BaseDirectory, "shaders");
        _rendererPath = Path.Combine(AppContext.BaseDirectory, "generated", "shaderRenderer.js");
    }

    public async Task<byte[]> ApplyAsync(IPage page, byte[] image, string shader)
    {
        Console.WriteLine($"Applying shader: {shader}");

        var shaderPath = Path.Combine(_shaderDirectory, $"{shader}.frag");

        if (!File.Exists(shaderPath)) {
            throw new FileNotFoundException($"Shader '{shader}' was not found.", shaderPath);
        }

        var source = await File.ReadAllTextAsync(shaderPath);

        Console.WriteLine($"Loaded shader: {shaderPath}");

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
                return await ShaderRenderer.renderShader(args);
            }
            """,
            new {
                imageBase64,
                fragmentSource = source
            });

        Console.WriteLine("Shader execution complete.");

        return result;
    }
}