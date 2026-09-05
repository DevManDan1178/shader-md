using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Formats;

namespace ShaderMarkdown.Exporting;

public static class GifBuilder {
    /// <summary>
    /// Gif delays have a unit of 0.01s.
    /// This is the inverse, used for calculating the time between frames (TBF).
    /// TBF = 1/FPS * GIF_FRAME_DELAY_OFFSET (TBF is in units of 0.01s)
    /// </summary>
    private const float FRAME_DELAY_OFFSET = 1 / 0.01f;

    /// <summary>
    /// Due to TBF unit having a lower bound,
    /// Max FPS is the inverse, so it is equivalent to FRAME_DELAY_OFFSET.
    /// </summary>
    private const int MAX_POSSIBLE_FPS = (int) FRAME_DELAY_OFFSET;

    public static async Task SaveAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath) {
        if (fps > MAX_POSSIBLE_FPS) {
            fps = MAX_POSSIBLE_FPS;
        }
        
        int frameDelay = Math.Max(1, (int) Math.Round(FRAME_DELAY_OFFSET / fps));

        using Image<Rgba32> gif = await LoadFrameAsync(frames[0]);
        
        ConfigureMetadata(gif, frameDelay);

        for (int i = 1; i < frames.Count; i++) {
            using Image<Rgba32> frame = await LoadFrameAsync(frames[i]);

            if (frame.Width != gif.Width || frame.Height != gif.Height) {
                throw new ArgumentException(
                    $"Frame {i} has dimensions {frame.Width}x{frame.Height}, " +
                    $"expected {gif.Width}x{gif.Height}.",
                    nameof(frames)
                );
            }

            ConfigureMetadata(frame, frameDelay);
            gif.Frames.AddFrame(frame.Frames.RootFrame);
        }

        await gif.SaveAsGifAsync(outputPath, CreateEncoder());
    }
    private static async Task<Image<Rgba32>> LoadFrameAsync(byte[] data) {
        using var stream = new MemoryStream(data);
    return await Image.LoadAsync<Rgba32>(stream);
}
    private static GifEncoder CreateEncoder() {
        return new GifEncoder {
            ColorTableMode = FrameColorTableMode.Global,
            Quantizer = new WuQuantizer(
                new QuantizerOptions {
                    MaxColors = 256,
                    Dither = null,
                    ColorMatchingMode = ColorMatchingMode.Exact,
                    TransparentColorMode = TransparentColorMode.Clear
                }
            )
        };
    }

    private static void ConfigureMetadata(Image<Rgba32> image, int frameDelay) {
        image.Metadata.GetGifMetadata().RepeatCount = 0; // 0 = Loop forever
        image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = frameDelay;
    }
}