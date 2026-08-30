using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace ShaderMarkdown.Rendering;

public static class GifBuilder {
    /// <summary>
    /// Gifs delays have a unit of 0.01s
    /// This is the inverse, used for calculating the time between frames (TBF)
    /// TBF = 1/FPS * GIF_FRAME_DELAY_OFFSET (TBF is in units of 0.01s)
    /// </summary>
    private const float GIF_FRAME_DELAY_OFFSET = 1/0.01f;
    /// <summary>
    /// Due to TBF unit being 0.01s, lowest TBF is 0.01s
    /// Max FPS is the inverse, so it is equivalent to GIF_FRAME_DELAY_OFFSET
    /// </summary>
    private const int MAX_POSSIBLE_FPS = (int) GIF_FRAME_DELAY_OFFSET;
    public static async Task SaveGifAsync(IReadOnlyList<byte[]> frames, int fps, string outputPath) {
        if (frames.Count == 0) {
            throw new ArgumentException("No frames were provided.", nameof(frames));
        }

        if (fps <= 0)  {
            throw new ArgumentOutOfRangeException(nameof(fps), "FPS must be greater than zero.");
        }

        if (fps > MAX_POSSIBLE_FPS) {
            fps = MAX_POSSIBLE_FPS;
        }

        using var gif = Image.Load(frames[0]);

        ConfigureMetadata(gif, fps);

        for (int i = 1; i < frames.Count; i++) {
            using var frame = Image.Load(frames[i]);

            ConfigureMetadata(frame, fps);

            gif.Frames.AddFrame(frame.Frames.RootFrame);
        }

        await gif.SaveAsGifAsync(outputPath, CreateEncoder());
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
                })
        };
    }

    private static void ConfigureMetadata(Image image, int fps) {
        int timeBetweenFrames = Math.Max(1, (int) Math.Round(GIF_FRAME_DELAY_OFFSET / fps));

        image.Metadata.GetGifMetadata().RepeatCount = 0; // 0 = Loop forever
        image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = timeBetweenFrames;
    }
}