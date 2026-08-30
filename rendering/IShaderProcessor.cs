using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShaderMarkdown.Rendering;
/// <summary>
/// Interface containing functions to process shaders to images (byte[])
/// </summary>
public interface IShaderProcessor {
    async Task<byte[][]> ApplyAnimatedAsync(IPage page, byte[] image, string shader, int framesPerSecond, float duration) {
        if (framesPerSecond <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }
        
        if (duration <= 0) {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
            
        int frameCount = (int) (framesPerSecond * duration);
        byte[][] frames = new byte[frameCount][];
        await LoadPageShaderScript(page);
        for (int frame = 0; frame < frameCount; ++frame) {
            float time = (float) frame / (float) framesPerSecond;
            var thisFrame = await ApplyAsync(page, image, shader, new ShaderParameters {
                Time = time,
            });
            frames[frame] = thisFrame;
        }

        return frames;
    }

    Task LoadPageShaderScript(IPage page);
    async Task<byte[][]> ApplyAnimatedToRectAsync(IPage page, int width, int height, string shader, int framesPerSecond, float duration, string color = "#FFFFFF") {
        if (width <= 0) {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0) {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
        
        using var image = new Image<Rgba32>(width, height);
        var parsedColor = Color.ParseHex(color.Trim());

        image.Mutate(ctx => ctx.BackgroundColor(parsedColor));

        await using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);

        return await ApplyAnimatedAsync(
            page,
            stream.ToArray(),
            shader,
            framesPerSecond,
            duration
        );
    }

    Task<byte[]> ApplyAsync(IPage page, byte[] image, string shader, ShaderParameters shaderParameters);
}


public class ShaderParameters {
    public float Time { get; set; }
}