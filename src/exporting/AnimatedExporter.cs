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
    /// <returns>The output path (if successful)</returns>
    public static async Task<string?> ExportAnimatedAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath, AnimatedFileExtension outputExtension) {
        if (frames.Count == 0) {
            throw new ArgumentException("No frames were provided.", nameof(frames));
        }

        if (fps <= 0) {
            throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be greater than zero.");
        }
        
        AnimatedFileExtension? fileExtension = FileExtension.GetAnimatedFileExtension(outputPath);
        if (fileExtension != outputExtension) {
            string? fileExtensionStr = FileExtension.GetFileExtensionString(outputExtension);
            if (fileExtensionStr != null) {
                outputPath += $".{fileExtensionStr}";
            }
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

        return outputPath;
    }
}