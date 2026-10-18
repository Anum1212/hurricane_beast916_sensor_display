using System.Drawing.Imaging;

namespace HurricaneBeastPanel;

/// <summary>Helpers drivers can reuse when turning a drawn picture into what a screen wants.</summary>
public static class FrameTools
{
    static readonly ImageCodecInfo JpegCodec =
        ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");

    /// <summary>Encodes as JPEG, lowering quality until the frame fits the screen's size limit.</summary>
    public static byte[] EncodeJpeg(Bitmap bmp, int maxBytes)
    {
        int quality = 90;
        return EncodeJpeg(bmp, maxBytes, ref quality);
    }

    /// <summary>
    /// Same, but starts from the quality that worked last time so animated themes do not re-encode several times per frame.
    /// The quality creeps back up when frames come in well under the limit.
    /// </summary>
    public static byte[] EncodeJpeg(Bitmap bmp, int maxBytes, ref int quality)
    {
        for (int q = Math.Clamp(quality, 20, 92); ; q -= 5)
        {
            using var parms = new EncoderParameters(1);
            parms.Param[0] = new EncoderParameter(Encoder.Quality, (long)q);
            using var ms = new MemoryStream();
            bmp.Save(ms, JpegCodec, parms);
            if (ms.Length <= maxBytes || q <= 20)
            {
                quality = ms.Length < maxBytes * 0.6 ? Math.Min(q + 2, 92) : q;
                return ms.ToArray();
            }
        }
    }

    /// <summary>Darkens a picture with a black layer. 100 leaves it unchanged, 0 makes it black.</summary>
    public static void Dim(Bitmap frame, int brightness)
    {
        int alpha = (int)Math.Round((100 - Math.Clamp(brightness, 0, 100)) / 100.0 * 255);
        if (alpha <= 0) return;
        using var g = Graphics.FromImage(frame);
        using var black = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
        g.FillRectangle(black, 0, 0, frame.Width, frame.Height);
    }

    /// <summary>Raw 16-bit RGB565 pixels, row by row (a common format for small SPI/USB screens).</summary>
    public static byte[] ToRgb565(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var src = new byte[Math.Abs(data.Stride) * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, src, 0, src.Length);
            var dst = new byte[bmp.Width * bmp.Height * 2];
            int o = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    int i = y * data.Stride + x * 4; // BGRA
                    int v = ((src[i + 2] & 0xF8) << 8) | ((src[i + 1] & 0xFC) << 3) | (src[i] >> 3);
                    dst[o++] = (byte)(v >> 8);
                    dst[o++] = (byte)(v & 0xFF);
                }
            return dst;
        }
        finally { bmp.UnlockBits(data); }
    }
}
