using ShaderMarkdown.Files;

namespace ShaderMarkdown.Exporting;

public static class AnimatedExporter {
    /// <summary>
    /// Exports the animated frames to the output path, adapting the format with the file extension.
    /// If the file extension is not supported, will not export
    /// </summary>
    /// <param name="frames">frames to export</param>
    /// <param name="fps"></param>FPS of the animation
    /// <param name="outputPath">Output path of the exported animation</param>
    /// <returns>(bool: success status, string: possible error message)</returns>
    public static async Task<(bool, string?)> ExportAnimatedAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath) {
        if (frames.Count == 0) {
            throw new ArgumentException("No frames were provided.", nameof(frames));
        }

        if (fps <= 0) {
            throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be greater than zero.");
        }
        
        AnimatedFileExtension? fileExtension = FileExtension.GetAnimatedFileExtension(outputPath);
        if (fileExtension == null) {
            return (false, $"Unsupported file extension: \"{outputPath}\".");
        }
        switch (fileExtension) {
            case AnimatedFileExtension.GIF:
                await GifBuilder.SaveAsync(frames, fps, outputPath);
                return (true, "");
            case AnimatedFileExtension.WEBP:
                await WebPBuilder.SaveAsync(frames, fps, outputPath);
                return (true, "");
            case AnimatedFileExtension.APNG:
                await APNGBuilder.SaveAsync(frames, fps, outputPath);
                return (true, "");
            default:
                break;
        }

        return (true, "");
    }
}