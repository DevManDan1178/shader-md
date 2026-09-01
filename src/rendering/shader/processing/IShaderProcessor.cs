using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShaderMarkdown.Rendering;
/// <summary>
/// Interface containing functions to process shaders to images (byte[])
/// </summary>
public interface IShaderProcessor {
    const int MAX_SHADER_TASKS_COUNT = 5;
    const int SHADER_THREADS_COUNT = 4;
    const int MINIMUM_MULTITHREADING_FRAME_COUNT = 10;
    float GetShaderTime(float initialTime, float timeScale, int frame, int framesPerSecond) {
        return initialTime + timeScale * ((float) frame / framesPerSecond);
    }

    int GetShaderFrameCount(int framesPerSecond, float duration) {
        return (int) Math.Max(Math.Round(framesPerSecond * duration), 1);
    }

    /// <summary>
    /// Applies the shader to an animated series of images
    /// </summary>
    /// <param name="browserContext"></param>
    /// <param name="image"></param>
    /// <param name="framesPerSecond"></param>
    /// <param name="duration"></param>
    /// <param name="shaderInfo"></param>
    /// <returns>Frames of the animated shader over the frames with the shader's corresponding time</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    async Task<byte[][]> ApplyOverAnimatedAsync(
        IBrowserContext browserContext, 
        byte[][] frames, 
        int framesPerSecond, 
        ShaderInfo shaderInfo
    ) {
        const int MIN_FRAMES_PER_WORKER = 4; 
        if (framesPerSecond <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }

        byte[][] shaderizedFrames = new byte[frames.Length][];
        
        // Cannot shortcut if timescale == 0 since initial frames can be different
        float timeScale = shaderInfo.ShaderParameters.TimeScale;
        float shaderInitialTime = shaderInfo.ShaderParameters.Time;  
        
        int workerCount = frames.Length < MINIMUM_MULTITHREADING_FRAME_COUNT ? 1 : Math.Min(SHADER_THREADS_COUNT, frames.Length);
        
        await Task.WhenAll(
            Enumerable.Range(0, workerCount)
                .Select(async (workerIdx) => {
                    IPage page = await browserContext.NewPageAsync();
                    await LoadPageShaderRenderer(page);

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

    
    /// <summary>
    /// Applies the shader to a static image
    /// </summary>
    /// <param name="browserContext"></param>
    /// <param name="image"></param>
    /// <param name="framesPerSecond"></param>
    /// <param name="duration"></param>
    /// <param name="shaderInfo"></param>
    /// <returns>Frames of the animated shader over the duration with the fps</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    async Task<byte[][]> ApplyOverStaticAsync(
        IBrowserContext browserContext, 
        byte[] image, 
        int framesPerSecond, 
        float duration,
        ShaderInfo shaderInfo
    ) {
        const int MIN_FRAMES_PER_WORKER = 10;
        if (framesPerSecond <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }
        
        if (duration <= 0) {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
            
        
        float timeScale = shaderInfo.ShaderParameters.TimeScale;
        float shaderInitialTime = shaderInfo.ShaderParameters.Time;

        byte[][] frames = new byte[GetShaderFrameCount(framesPerSecond, duration)][];
        if (timeScale == 0) {
            // If the shader is frozen, call once using regular shader renderer and clone frames
            IPage page = await browserContext.NewPageAsync();
            await LoadPageShaderRenderer(page);

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

        
        int workerCount = Math.Max(1, Math.Min(MAX_SHADER_TASKS_COUNT, frames.Length / MIN_FRAMES_PER_WORKER));
        
        float[] shaderTimes = Enumerable.Range(0, frames.Length).Select(
            frame => GetShaderTime(shaderInitialTime, timeScale, frame, framesPerSecond)
        ).ToArray();
        
        if (workerCount <= 1)  {
            IPage page = await browserContext.NewPageAsync();
            await LoadPageStaticShaderRenderer(page);
            
            frames = await ApplyStaticBatchAsync(page, image, shaderInfo, shaderTimes);

            await page.CloseAsync();
            return frames;
        }
        
        // No race conditions since every thread writes to different frames independently
        
        await Task.WhenAll(
            Enumerable.Range(0, workerCount)
                .Select(async (workerIdx) => {
                    IPage page = await browserContext.NewPageAsync();
                    
                    await LoadPageStaticShaderRenderer(page);

                    var frameIndices = new List<int>();
                    for (int frame = workerIdx; frame < frames.Length; frame += workerCount) {
                        frameIndices.Add(frame);
                    }

                    float[] workerShaderTimes = frameIndices.Select(
                        frame => shaderTimes[frame]
                    ).ToArray();

                    byte[][] workerResults = await ApplyStaticBatchAsync(page, image, shaderInfo, workerShaderTimes);

                    for (int i = 0; i < frameIndices.Count; i++) {
                        frames[frameIndices[i]] = workerResults[i];
                    }

                    await page.CloseAsync();
                }
            )
        );

        return frames;
    }
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

        return await ApplyOverStaticAsync(
            context,
            stream.ToArray(), 
            framesPerSecond,
            duration,
            shaderInfo
        );
    }

    Task LoadPageShaderRenderer(IPage page);
    Task LoadPageStaticShaderRenderer(IPage page);

    /// <summary>
    /// Applies the shader to the image
    /// </summary>
    /// <param name="page">HTML page to run the shader scripts</param>
    /// <param name="image">Image to shaderize, as an array of bytes</param>
    /// <param name="shaderInfo">The shader information</param>
    /// <returns></returns>
    Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float shaderTime);

    /// <summary>
    /// Applies the shader to ONE image across MANY points in time (and/or
    /// shader property sets), uploading that image to the GPU exactly once
    /// for the whole batch rather than once per requested frame.
    /// </summary>
    /// <param name="page">HTML page to run the shader scripts</param>
    /// <param name="image">The single image shared by every frame in this batch</param>
    /// <param name="shaderInfo">The shader information</param>
    /// <param name="shaderTimes">One time value per output frame</param>
    Task<byte[][]> ApplyStaticBatchAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float[] shaderTimes);

}

