using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShaderMarkdown.Exporting;

public static class WebPBuilder {
    /// <summary>
    /// WebP delays have a unit of 0.001s.
    /// This is the inverse, used for calculating the time between frames (TBF).
    /// TBF = 1/FPS * WebP_FRAME_DELAY_OFFSET (TBF is in units of 0.001s)
    /// </summary>
    const float FRAME_DELAY_OFFSET = 1f / 0.001f;
    /// <summary>
    /// Due to TBF unit having a lower bound
    /// Max FPS is the inverse, so it is equivalent to FRAME_DELAY_OFFSET.
    /// </summary>
    private const int MAX_POSSIBLE_FPS = (int) FRAME_DELAY_OFFSET;
    public static async Task SaveAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath) {
        if (fps > MAX_POSSIBLE_FPS) {
            fps = MAX_POSSIBLE_FPS;
        }
        
        int frameDelayMs = Math.Max(1, (int) Math.Round(FRAME_DELAY_OFFSET / fps));

        using Image<Rgba32> webp = await LoadFrameAsync(frames[0]);
        ConfigureMetadata(webp, frameDelayMs);

        for (int i = 1; i < frames.Count; i++) {
            using Image<Rgba32> frame = await LoadFrameAsync(frames[i]);

            if (frame.Width != webp.Width || frame.Height != webp.Height) {
                throw new ArgumentException(
                    $"Frame {i} has dimensions {frame.Width}x{frame.Height}, " +
                    $"expected {webp.Width}x{webp.Height}.",
                    nameof(frames)
                );
            }

            ConfigureMetadata(frame, frameDelayMs);
            webp.Frames.AddFrame(frame.Frames.RootFrame);
        }

        await webp.SaveAsWebpAsync(outputPath, CreateEncoder());
    }

    private static async Task<Image<Rgba32>> LoadFrameAsync(byte[] data) {
        using var stream = new MemoryStream(data);
        return await Image.LoadAsync<Rgba32>(stream);
    }

    private static WebpEncoder CreateEncoder() {
        return new WebpEncoder {
            FileFormat = WebpFileFormatType.Lossless,
        };
    }

    private static void ConfigureMetadata(Image<Rgba32> image, int frameDelay) {
        image.Metadata.GetWebpMetadata().RepeatCount = 0; // 0 = loop forever
        image.Frames.RootFrame.Metadata.GetWebpMetadata().FrameDelay = (uint) frameDelay;
    }
}