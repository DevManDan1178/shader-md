
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public static class ImageSlicer {
    
    /// <summary>
    /// Separates the animated frames into vertical slices
    /// </summary>
    /// <param name="frames">The frames as an array of images (which are an array of bytes)</param>
    /// <param name="sliceCount">
    /// /// The amount of vertical slices 
    /// [Throws ArgumentOutOfRangeException if <= 1]
    /// </param>
    /// <returns>
    /// The vertically sliced animated frames.
    /// SliceFramesVertically(...)[i] = (i + 1)'th animated vertical slice from the left
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static byte[][][] SliceFramesVertically(byte[][] frames, int sliceCount) {
        if (sliceCount <= 1) {
            throw new ArgumentOutOfRangeException($"Invalid image slice count: {nameof(sliceCount)}.");
        }

        byte[][][] slicedFrames = new byte[sliceCount][][];
        for (int i = 0; i < sliceCount; ++i) {
            slicedFrames[i] = new byte[frames.Length][];
        }

        Parallel.For(0, frames.Length, frameIdx => {
            byte[][] frameSlices = SliceImageVertically(frames[frameIdx], sliceCount);

            for (int sliceIdx = 0; sliceIdx < frameSlices.Length; ++sliceIdx) {
                slicedFrames[sliceIdx][frameIdx] = frameSlices[sliceIdx];
            }
        });
        
        return slicedFrames;   
    }
    /// <summary>
    /// Separates the image into vertical slices
    /// </summary>
    /// <param name="pngBytes">The image as an array of byte</param>
    /// <param name="sliceCount">
    /// The amount of vertical slices 
    /// [Throws ArgumentOutOfRangeException if <= 1]
    /// </param>
    /// <returns>
    /// The vertically sliced images. 
    /// SliceVertically(...)[i] = (i + 1)'th vertical slice from the left
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static byte[][] SliceImageVertically(byte[] imageBytes, int sliceCount) {
        if (sliceCount <= 1) {
            throw new ArgumentOutOfRangeException(nameof(sliceCount), "Invalid image slice count.");
        }

        using var image = Image.Load<Rgba32>(imageBytes);

        int sliceWidth = image.Width / sliceCount;
        var slices = new byte[sliceCount][];

        for (int i = 0; i < sliceCount; ++i) {
            int x = i * sliceWidth;
            int w = (i == sliceCount - 1) ? image.Width - x : sliceWidth;

            using var slice = image.Clone(ctx => ctx.Crop(new Rectangle(x, 0, w, image.Height)));

            using var ms = new MemoryStream();
            slice.SaveAsPng(ms);
            slices[i] = ms.ToArray();
        }

        return slices;
    }
}