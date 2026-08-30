using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShaderMarkdown.Rendering;
/// <summary>
/// Interface containing functions to process shaders to images (byte[])
/// </summary>
public interface IShaderProcessor {
    async Task<byte[][]> ApplyAnimatedAsync(
        IPage page, 
        byte[] image, 
        ShaderInfo shaderInfo, 
        int framesPerSecond, 
        float duration
    ) {
        if (framesPerSecond <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }
        
        if (duration <= 0) {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
            
        int frameCount = (int) (framesPerSecond * duration);
        byte[][] frames = new byte[frameCount][];
        await LoadPageShaderScript(page);
        ShaderParameters shaderParameters = shaderInfo.ShaderParameters;
        for (int frame = 0; frame < frameCount; ++frame) {  
            if (shaderParameters.InterpolateTime) {
                shaderParameters.Time = (float) frame / (float) framesPerSecond;
            }
            var thisFrame = await ApplyAsync(page, image, shaderInfo);
            frames[frame] = thisFrame;
        }

        return frames;
    }

    Task LoadPageShaderScript(IPage page);
    async Task<byte[][]> ApplyAnimatedToRectAsync(
        IPage page, 
        int width, 
        int height, 
        ShaderInfo shaderInfo, 
        int framesPerSecond, 
        float duration, 
        string color = "#FFFFFF"
    ) {
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
            shaderInfo,
            framesPerSecond,
            duration
        );
    }

    Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo);
}

