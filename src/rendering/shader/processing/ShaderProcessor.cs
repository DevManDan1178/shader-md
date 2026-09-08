using System.Text.Json;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ShaderMarkdown.Files;
using SixLabors.ImageSharp.Processing;

namespace ShaderMarkdown.Rendering;

/// <summary>
/// Implementation of the IShaderProcessor interface
/// Implements the application of a shader to an image
/// </summary>
public class ShaderProcessor : IShaderProcessor {
    private sealed class ImageSliceInfo {
        public int VerticalSliceCount { get; set; }
        public int HorizontalSliceCount { get; set; }
        public int VerticalSliceIndex { get; set; }
        public int HorizontalSliceIndex { get; set; }
    }
    private sealed class RawFrameResult {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Pixels { get; set; } = [];
    }
    public bool MultithreadingEnabled { get; init; } = true;
    public ShaderProcessor(bool multithreadingEnabled = true) {   
        MultithreadingEnabled  = multithreadingEnabled;
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
        if (!File.Exists(WebScriptPaths.SHADER_RENDERER)) {
            throw new FileNotFoundException($"Shader renderer JavaScript not found: \"{WebScriptPaths.SHADER_RENDERER}\".");
        }
        await LoadPageScript(page, WebScriptPaths.SHADER_RENDERER, PAGE_SHADER_RENDERER_LOADED_FLAG);
    }

    public async Task LoadPageStaticShaderRenderer(IPage page) {
        if (!File.Exists(WebScriptPaths.STATIC_SHADER_RENDERER)) {
            throw new FileNotFoundException($"Static shader renderer JavaScript not found: \"{WebScriptPaths.STATIC_SHADER_RENDERER}\".");
        }
        await LoadPageScript(page, WebScriptPaths.STATIC_SHADER_RENDERER, PAGE_STATIC_SHADER_RENDERER_LOADED_FLAG);
    }

    private bool CheckValidShader(string shaderPath) {
        if (!File.Exists(shaderPath)) {
            throw new FileNotFoundException($"Shader not found: {shaderPath}\nAre you sure \"{Path.GetFileName(shaderPath)}\" is the correct file name?");
        }
        return !(shaderPath.Trim() == "") && File.Exists(shaderPath);
    }

    /// <summary>
    /// Chunking threshold: images at or under this size render in a single pass
    /// larger images get split into a grid via ApplyChunkedAsync.
    /// </summary>
    const long MAX_DIRECT_RENDER_SIZE_BYTES = 64 * 1024L * 1024L;
    public bool CheckExcessiveImageSize(long imageSizeBytes, int imageWidth, int imageHeight) {
        return imageSizeBytes > MAX_DIRECT_RENDER_SIZE_BYTES || imageWidth > MAX_TEXTURE_DIMENSION || imageHeight > MAX_TEXTURE_DIMENSION;
    }
    public async Task<byte[]> ApplyAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float shaderTime) {
        if (!CheckValidShader(shaderInfo.ShaderPath)) {
            return image;
        }

        var imageInfo = Image.Identify(image);
        long imageSizeBytes = (long)imageInfo.Width * imageInfo.Height * 4;

        // in ApplyAsync's chunking branch:
        if (CheckExcessiveImageSize(imageSizeBytes, imageInfo.Width, imageInfo.Height)) {
            return await ApplyChunkedAsync(page, image, shaderInfo, shaderTime);
        }

        if (! await page.EvaluateAsync<bool>(
            $"() => !!window.{PAGE_SHADER_RENDERER_LOADED_FLAG}"
        )) {
            throw new Exception("Shader renderer not loaded."); 
        }
        
        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);


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

    const long MAX_BATCH_SIZE_BYTES = 256 * 1024L * 1024L;
    public async Task<byte[][]> ApplyStaticBatchAsync(IPage page, byte[] image, int imageWidth, int imageHeight, ShaderInfo shaderInfo, float[] shaderTimes) {
        if (!CheckValidShader(shaderInfo.ShaderPath)) {
            byte[][] frames = new byte[shaderTimes.Length][];
            for (int i = 0; i < frames.Length; ++i) {
                frames[i] = image;
            }
            return frames;
        }
        if (imageWidth <= 0 || imageHeight <= 0) {
            throw new ArgumentOutOfRangeException("Image dimensions must be greater than zero.");
        }
        if (shaderTimes.Length == 0) {
            return [];
        }

        long imageSizeBytes = (long) imageWidth * imageHeight * 4;

        if (CheckExcessiveImageSize(imageSizeBytes, imageWidth, imageHeight)) {     
            using var sourceImage = Image.Load<Rgba32>(image);
            var (verticalSliceCount, horizontalSliceCount) = ComputeSliceCounts(sourceImage.Width, sourceImage.Height);

            var chunkedOutput = new byte[shaderTimes.Length][];
            for (int i = 0; i < shaderTimes.Length; i++) {
                chunkedOutput[i] = await ApplyChunkedAsync(page, sourceImage, shaderInfo, shaderTimes[i], verticalSliceCount, horizontalSliceCount);
            }
            return chunkedOutput;
        }

        if (! await page.EvaluateAsync<bool>(
            $"() => !!window.{PAGE_STATIC_SHADER_RENDERER_LOADED_FLAG}"
        )) {
            throw new Exception("Static shader renderer not loaded."); 
        }

        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);
        
        var imageBase64 = Convert.ToBase64String(image);
        var shaderPropertiesJson = JsonSerializer.Serialize(shaderInfo.ShaderParameters.ShaderProperties);

        long frameSizeBytes = (long)imageWidth * imageHeight * 4;
        int framesPerBatch = Math.Max(1, (int)(MAX_BATCH_SIZE_BYTES / frameSizeBytes));

        RawFrameResult[] results = new RawFrameResult[shaderTimes.Length];
        for (int batchStart = 0; batchStart < shaderTimes.Length; batchStart += framesPerBatch) {
            
            int batchSize = Math.Min(
                framesPerBatch,
                shaderTimes.Length - batchStart
            );

            var batchFrames = new object[batchSize];

            for (int i = 0; i < batchSize; i++) {;
                batchFrames[i] = new {
                    time = (double) shaderTimes[batchStart + i], // Breaks shaders when not casting to (double)
                    shaderProperties = shaderPropertiesJson
                };
            }

            RawFrameResult[] batchResults = await page.EvaluateAsync<RawFrameResult[]>(
                """
                    async (args) => {
                        args.frames = args.frames.map((val) => ({
                            time: val.time,
                            shaderProperties: JSON.parse(val.shaderProperties)
                        }));

                        return await StaticShaderRenderer.renderStaticShaderBatchRaw(args);
                    }
                """,
                new {
                    imageBase64,
                    fragmentSource = source,
                    shaderPath = shaderInfo.ShaderPath,
                    frames = batchFrames
                }
            );
        

            Array.Copy(
                batchResults,
                0,
                results,
                batchStart,
                batchResults.Length
            );
        }
                
        var output = new byte[results.Length][];

        for (int i = 0; i < results.Length; i++) {
            using var img = Image.LoadPixelData<Rgba32>(results[i].Pixels, results[i].Width, results[i].Height);
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            output[i] = ms.ToArray();
        }

        return output;
    }


    public async Task<byte[]> ApplyChunkedAsync(IPage page, byte[] image, ShaderInfo shaderInfo, float shaderTime) {
        if (!CheckValidShader(shaderInfo.ShaderPath)) {
            return image;
        }
        using var sourceImage = Image.Load<Rgba32>(image);
    
        var (verticalSliceCount, horizontalSliceCount) = ComputeSliceCounts(sourceImage.Width, sourceImage.Height);
        return await ApplyChunkedAsync(page, sourceImage, shaderInfo, shaderTime, verticalSliceCount, horizontalSliceCount);
    }

    private async Task<byte[]> ApplyChunkedAsync(IPage page, Image<Rgba32> sourceImage, ShaderInfo shaderInfo, float shaderTime, int verticalSliceCount, int horizontalSliceCount) {
        if (! await page.EvaluateAsync<bool>(
            $"() => !!window.{PAGE_SHADER_RENDERER_LOADED_FLAG}"
        )) {
            throw new Exception("Shader renderer not loaded");
        }
        if (verticalSliceCount <= 0 || horizontalSliceCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(verticalSliceCount), "Slice counts must be greater than zero.");
        }

        var source = await File.ReadAllTextAsync(shaderInfo.ShaderPath);

        var shaderPropertiesJson = JsonSerializer.Serialize(shaderInfo.ShaderParameters.ShaderProperties);

        int width = sourceImage.Width;
        int height = sourceImage.Height;
        
        var columnBounds = ComputeAxisBounds(width, verticalSliceCount);
        var rowBounds = ComputeAxisBounds(height, horizontalSliceCount);

        var chunkResults = new RawFrameResult[verticalSliceCount, horizontalSliceCount];

        for (int col = 0; col < verticalSliceCount; col++) {
            int chunkX = columnBounds[col];
            int chunkWidth = columnBounds[col + 1] - columnBounds[col];

            for (int row = 0; row < horizontalSliceCount; row++) {
                int chunkY = rowBounds[row];
                int chunkHeight = rowBounds[row + 1] - rowBounds[row];

                using var chunkImage = sourceImage.Clone(ctx => ctx.Crop(new Rectangle(chunkX, chunkY, chunkWidth, chunkHeight)));
                
                using var chunkMs = new MemoryStream();
                await chunkImage.SaveAsPngAsync(chunkMs);
                var chunkBase64 = Convert.ToBase64String(chunkMs.ToArray());

                RawFrameResult chunk = await page.EvaluateAsync<RawFrameResult>(
                    """
                    async (args) => {
                        args.parameters.shaderProperties = JSON.parse(args.parameters.shaderProperties);

                        return await ShaderRenderer.renderShaderChunkRaw(args);
                    }
                    """,
                    new {
                        imageBase64 = chunkBase64,
                        fragmentSource = source,
                        shaderPath = shaderInfo.ShaderPath,
                        parameters = new {
                            time = (double) shaderTime, // Breaks shaders when not casting to (double)
                            shaderProperties = shaderPropertiesJson,
                        },
                        imageSliceInfo = new {
                            verticalSliceCount,
                            horizontalSliceCount,
                            verticalSliceIndex = col,
                            horizontalSliceIndex = row
                        }
                    }
                );

                chunkResults[col, row] = chunk;
            }
        }

        using var outputImage = new Image<Rgba32>(width, height);

        for (int col = 0; col < verticalSliceCount; col++) {
            for (int row = 0; row < horizontalSliceCount; row++) {
                var chunk = chunkResults[col, row];
                using var renderedChunkImage = Image.LoadPixelData<Rgba32>(chunk.Pixels, chunk.Width, chunk.Height);
                outputImage.Mutate(ctx => ctx.DrawImage(renderedChunkImage, new Point(columnBounds[col], rowBounds[row]), 1f));
            }
        }

        using var ms = new MemoryStream();
        await outputImage.SaveAsPngAsync(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Splits [0, totalSize) into sliceCount contiguous integer-pixel segments as
    /// evenly as possible, returning the boundary offsets (length sliceCount + 1).
    /// </summary>
    private static int[] ComputeAxisBounds(int totalSize, int sliceCount) {
        var bounds = new int[sliceCount + 1];
        for (int i = 0; i <= sliceCount; i++) {
            bounds[i] = (int)((long)totalSize * i / sliceCount);
        }
        return bounds;
    }

    const int MAX_TEXTURE_DIMENSION = 8192;

    /// <summary>
    /// Determines how many vertical/horizontal slices are needed to keep every
    /// chunk within both the byte-size budget and the max texture dimension.
    /// </summary>
    private static (int verticalSliceCount, int horizontalSliceCount) ComputeSliceCounts(int width, int height) {
        long totalBytes = (long)width * height * 4;
        int slicesNeededForSize = (int)Math.Ceiling((double)totalBytes / MAX_DIRECT_RENDER_SIZE_BYTES);
        int baseSliceCountPerAxis = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(slicesNeededForSize)));

        // Independently ensure no single axis chunk exceeds MAX_TEXTURE_DIMENSION,
        // since byte-size slicing alone doesn't protect against extreme aspect ratios.
        int verticalSliceCount = Math.Max(baseSliceCountPerAxis, (int)Math.Ceiling((double)width / MAX_TEXTURE_DIMENSION));
        int horizontalSliceCount = Math.Max(baseSliceCountPerAxis, (int)Math.Ceiling((double)height / MAX_TEXTURE_DIMENSION));

        return (verticalSliceCount, horizontalSliceCount);
    }
}