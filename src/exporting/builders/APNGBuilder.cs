using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ShaderMarkdown.Exporting;

public static class APNGBuilder {
    public static async Task SaveAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath) {
        int frameDelayMs = Math.Max(1, (int) Math.Round(1000.0 / fps));

        using Image<Rgba32> apng = await LoadFrameAsync(frames[0]);
        ConfigureRootMetadata(apng);
        ConfigureFrameMetadata(apng, frameDelayMs);

        for (int i = 1; i < frames.Count; i++) {
            using Image<Rgba32> frame = await LoadFrameAsync(frames[i]);

            if (frame.Width != apng.Width || frame.Height != apng.Height) {
                throw new ArgumentException(
                    $"Frame {i} has dimensions {frame.Width}x{frame.Height}, " +
                    $"expected {apng.Width}x{apng.Height}.",
                    nameof(frames)
                );
            }

            ConfigureFrameMetadata(frame, frameDelayMs);
            apng.Frames.AddFrame(frame.Frames.RootFrame);
        }

        await apng.SaveAsPngAsync(outputPath, CreateEncoder());
    }

    private static async Task<Image<Rgba32>> LoadFrameAsync(byte[] data) {
        using var stream = new MemoryStream(data);
        return await Image.LoadAsync<Rgba32>(stream);
    }

    private static PngEncoder CreateEncoder() {
        return new PngEncoder {
            ColorType = PngColorType.RgbWithAlpha
        };
    }

    private static void ConfigureRootMetadata(Image<Rgba32> image) {
        var meta = image.Metadata.GetPngMetadata();
        meta.AnimateRootFrame = true; // include the first frame in the animated sequence
        meta.RepeatCount = 0;         // 0 = loop forever
    }

    private static void ConfigureFrameMetadata(Image<Rgba32> image, int frameDelayMs) {
        var frameMeta = image.Frames.RootFrame.Metadata.GetPngMetadata();
        frameMeta.FrameDelay = new Rational((uint) frameDelayMs, 1000);
    }
}