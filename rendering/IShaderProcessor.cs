using Microsoft.Playwright;

namespace ShaderMarkdown.Rendering;

public interface IShaderProcessor {
    Task<byte[]> ApplyAsync(IPage page, byte[] image, string shader);
}
