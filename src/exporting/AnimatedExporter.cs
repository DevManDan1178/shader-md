using ShaderMarkdown.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShaderMarkdown.Exporting;

public static class AnimatedExporter {
    /// <summary>
    /// Exports the animated frames to the output path, adapting the format with the file extension.
    /// If the file extension is not supported, will not export
    /// </summary>
    /// <param name="frames">frames to export</param>
    /// <param name="fps"></param>FPS of the animation
    /// <param name="outputPath">Output path of the exported animation</param>
    /// <returns>Success status</returns>
    public static async Task ExportAnimatedAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath, AnimatedFileExtension outputExtension) {
        if (frames.Count == 0) {
            throw new ArgumentException("No frames were provided.", nameof(frames));
        }

        if (fps <= 0) {
            throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be greater than zero.");
        }
        
        switch (outputExtension) {
            case AnimatedFileExtension.GIF:
                await GifBuilder.SaveAsync(frames, fps, outputPath);
                break;
            case AnimatedFileExtension.WEBP:
                await WebPBuilder.SaveAsync(frames, fps, outputPath);
                break;
            case AnimatedFileExtension.APNG:
                await APNGBuilder.SaveAsync(frames, fps, outputPath);
                break;
            default:
                break;
        }
    }
}