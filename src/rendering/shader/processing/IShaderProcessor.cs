using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShaderMarkdown.Rendering;
/// <summary>
/// Interface containing functions to process shaders to images (byte[])
/// </summary>
public interface IShaderProcessor {
    const int SHADER_THREADS_COUNT = 4;
    const int MINIMUM_MULTITHREADING_FRAME_COUNT = 10;
    
    float GetShaderTime(float initialTime, float timeScale, int frame, int framesPerSecond) {
        return initialTime + timeScale * ((float) frame / framesPerSecond);
    }
    async Task<byte[][]> ApplyOverAnimatedAsync(
        IBrowserContext browserContext, 
        byte[][] frames, 
        int framesPerSecond, 
        float duration,
        ShaderInfo shaderInfo
    ) {
        byte[][] shaderizedFrames = new byte[frames.Length][];
        
        
        // Cannot shortcut if timescale == 0 since initial frames can be different
        float timeScale = shaderInfo.ShaderParameters.TimeScale;
        float shaderInitialTime = shaderInfo.ShaderParameters.Time;  
        
        int workerCount = frames.Length < MINIMUM_MULTITHREADING_FRAME_COUNT ? 1 : Math.Min(SHADER_THREADS_COUNT, frames.Length);

        await Task.WhenAll(
            Enumerable.Range(0, workerCount)
                .Select(async (workerIdx) => {
                    IPage page = await browserContext.NewPageAsync();
                    await LoadPageShaderScript(page);

                    for (int frame = 0 + workerIdx; frame < frames.Length; frame += workerCount) {
                        Console.WriteLine($"Shaderizing: document frame {frame + 1} of {frames.Length}.");
                        shaderizedFrames[frame] = await ApplyAsync(
                            page, 
                            frames[frame], 
                            shaderInfo, 
                            GetShaderTime(shaderInitialTime, timeScale, frame, framesPerSecond)
                        );
                    }

                    await page.CloseAsync();
                }
            )
        );
       
        return shaderizedFrames;
    }

    async Task<byte[][]> ApplyAnimatedAsync(
        IBrowserContext browserContext, 
        byte[] image, 
        int framesPerSecond, 
        float duration,
        ShaderInfo shaderInfo
    ) {
        if (framesPerSecond <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }
        
        if (duration <= 0) {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
            
        byte[][] frames = new byte[(int) (framesPerSecond * duration)][];
        
        float timeScale = shaderInfo.ShaderParameters.TimeScale;
        float shaderInitialTime = shaderInfo.ShaderParameters.Time;


        if (timeScale == 0) {
            // If the shader is frozen, call once and clone frames
            IPage page = await browserContext.NewPageAsync();
            await LoadPageShaderScript(page);

            byte[] shaderizedFrame = await ApplyAsync(
                page, 
                image, 
                shaderInfo, 
                shaderInitialTime + timeScale
            ); 
            
            for (int frame = 0; frame < frames.Length; ++frame) {
                frames[frame] = shaderizedFrame;
            }
            await page.CloseAsync();

            return frames;
        }

        
        int workerCount = frames.Length < MINIMUM_MULTITHREADING_FRAME_COUNT ? 1 : Math.Min(SHADER_THREADS_COUNT, frames.Length);

        // No race conditions since every thread writes to different frames independently
        await Task.WhenAll(
            Enumerable.Range(0, workerCount)
                .Select(async (workerIdx) => {
                    IPage page = await browserContext.NewPageAsync();
                    await LoadPageShaderScript(page);
                    
                    for (int frame = 0 + workerIdx; frame < frames.Length; frame += workerCount)  {   
                        frames[frame] = await ApplyAsync(
                            page, 
                            image, 
                            shaderInfo, 
                            GetShaderTime(shaderInitialTime, timeScale, frame, framesPerSecond)
                        ); 
                    }
                    await page.CloseAsync();
                }
            )
        );   

        return frames;
    }

    Task LoadPageShaderScript(IPage page);
    async Task<byte[][]> ApplyAnimatedToRectAsync(
        IBrowserContext context, 
        int width, 
        int height, 
        int framesPerSecond, 
        float duration, 
        ShaderInfo shaderInfo,
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
            context,
            stream.ToArray(), 
            framesPerSecond,
            duration,
            shaderInfo
        );
    }

    /// <summary>
    /// Applies the shader to the image
    /// </summary>
    /// <param name="page">HTML page to run the shader scripts</param>
    /// <param name="image">Image to shaderize, as an array of bytes</param>
    /// <param name="shaderInfo">The shader information</param>
    /// <returns></returns>
    Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float shaderTime);
}

