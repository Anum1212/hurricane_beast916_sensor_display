using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using OpenCvSharp;

namespace HurricaneBeastPanel;

/// <summary>Development helpers: generates small test media files so the media features can be checked without any sample files.</summary>
static class DevTools
{
    /// <summary>Builds a theme that shows every widget style of one category in a labelled grid (for visual checks).</summary>
    public static Theme Gallery(WidgetCategory category)
    {
        var items = WidgetCatalog.In(category).ToList();
        int cols = category == WidgetCategory.Graph ? 4 : 6;
        float cellW = category == WidgetCategory.Graph ? 440 : 300, cellH = category == WidgetCategory.Gauge ? 330 : category == WidgetCategory.Graph ? 260 : 360;
        int rows = (items.Count + cols - 1) / cols;
        var theme = new Theme { Name = "Gallery", Width = (int)(cols * cellW + 20), Height = (int)(rows * cellH + 20), Background = FillSpec.Solid("#FF000000") };
        for (int i = 0; i < items.Count; i++)
        {
            var info = items[i];
            float x = 10 + i % cols * cellW, y = 10 + i / cols * cellH;
            var el = Element.CreateWidget(info.Style, i + 1, theme.Width, theme.Height);
            float maxW = cellW - 40, maxH = cellH - 70;
            float scale = Math.Min(1f, Math.Min(maxW / el.Width, maxH / el.Height));
            el.Width = MathF.Round(el.Width * scale);
            el.Height = MathF.Round(el.Height * scale);
            el.X = MathF.Round(x + (cellW - el.Width) / 2);
            el.Y = MathF.Round(y + 10 + (maxH - el.Height) / 2);
            // Unbound widgets show built-in demo values (65% / 40% / 55%), which is what a design check needs.
            el.Bind = el.Bind2 = el.Bind3 = "";
            theme.Elements.Add(el);
            theme.Elements.Add(new Element
            {
                Type = ElementType.Text, Name = "label", Text = info.Name, X = x, Y = y + cellH - 56, Width = cellW, Height = 40,
                FontSize = 22, Align = HAlign.Center, Fill = FillSpec.Solid("#FFC8CDD8"),
            });
        }
        return theme;
    }

    /// <summary>Every date/time preset as live text, next to its format string.</summary>
    public static Theme DateGallery()
    {
        var entries = DateTimeFormats.Groups.SelectMany(g => g.Items).ToList();
        const int cols = 3, rowH = 34;
        int rows = (entries.Count + cols - 1) / cols;
        var theme = new Theme { Name = "Dates", Width = 1900, Height = rows * rowH + 20, Background = FillSpec.Solid("#FF000000") };
        for (int i = 0; i < entries.Count; i++)
        {
            float x = 10 + i % cols * 630, y = 10 + i / cols * rowH;
            theme.Elements.Add(new Element { Type = ElementType.Text, Name = "fmt", Text = entries[i].Format, X = x, Y = y, Width = 250, Height = rowH, FontSize = 17, Fill = FillSpec.Solid("#FF8C97AC") });
            theme.Elements.Add(new Element { Type = ElementType.Text, Name = "val", Text = DateTimeFormats.Token(entries[i].Format), X = x + 255, Y = y, Width = 360, Height = rowH, FontSize = 24, Bold = true, Fill = FillSpec.Solid("#FFFFFFFF") });
        }
        return theme;
    }

    public static void MakeTestMedia(string dir)
    {
        Directory.CreateDirectory(dir);
        MakeVideo(Path.Combine(dir, "test.mp4"));
        MakeGif(Path.Combine(dir, "test.gif"));
        using var bmp = Frame(0, 400, 300, "PNG");
        bmp.Save(Path.Combine(dir, "test.png"), ImageFormat.Png);
    }

    static Bitmap Frame(int n, int w, int h, string label)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(255, 30, 60, 160), Color.FromArgb(255, 200, 70, 120), 45f))
            g.FillRectangle(bg, 0, 0, w, h);
        float cx = w / 2f + (float)Math.Cos(n * 0.5) * w * 0.3f, cy = h / 2f + (float)Math.Sin(n * 0.5) * h * 0.3f;
        using (var b = new SolidBrush(Color.FromArgb(255, 255, 220, 60)))
            g.FillEllipse(b, cx - h * 0.18f, cy - h * 0.18f, h * 0.36f, h * 0.36f);
        using var font = new Font("Segoe UI", h / 6f, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString($"{label} {n}", font, Brushes.White, 8, 6);
        return bmp;
    }

    static void MakeVideo(string path)
    {
        const int w = 480, h = 270, count = 90;
        using var writer = new VideoWriter(path, FourCC.MP4V, 30, new OpenCvSharp.Size(w, h));
        if (!writer.IsOpened()) throw new IOException("VideoWriter could not open " + path);
        for (int i = 0; i < count; i++)
        {
            using var bmp = Frame(i, w, h, "MP4");
            using var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp);
            writer.Write(mat);
        }
    }

    /// <summary>Writes an animated GIF with a fixed 216-colour palette and trivial (literal-only) LZW coding.</summary>
    static void MakeGif(string path)
    {
        const int w = 200, h = 150, frames = 12;
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write("GIF89a"u8.ToArray());
        bw.Write((ushort)w); bw.Write((ushort)h);
        bw.Write((byte)0xF7); bw.Write((byte)0); bw.Write((byte)0); // global palette, 256 entries
        for (int i = 0; i < 256; i++)
        {
            if (i < 216) { bw.Write((byte)(i / 36 * 51)); bw.Write((byte)(i / 6 % 6 * 51)); bw.Write((byte)(i % 6 * 51)); }
            else { bw.Write((byte)0); bw.Write((byte)0); bw.Write((byte)0); }
        }
        bw.Write(new byte[] { 0x21, 0xFF, 0x0B });
        bw.Write("NETSCAPE2.0"u8.ToArray());
        bw.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 }); // loop forever

        for (int f = 0; f < frames; f++)
        {
            using var bmp = Frame(f, w, h, "GIF");
            var pixels = new byte[w * h];
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < h; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (int x = 0; x < w; x++)
                {
                    int b = row[x * 3], g = row[x * 3 + 1], r = row[x * 3 + 2];
                    pixels[y * w + x] = (byte)(((r + 25) / 51) * 36 + ((g + 25) / 51) * 6 + (b + 25) / 51);
                }
            }
            bmp.UnlockBits(data);

            bw.Write(new byte[] { 0x21, 0xF9, 0x04, 0x00 }); bw.Write((ushort)8); bw.Write(new byte[] { 0x00, 0x00 }); // 80 ms
            bw.Write((byte)0x2C); bw.Write((ushort)0); bw.Write((ushort)0); bw.Write((ushort)w); bw.Write((ushort)h); bw.Write((byte)0);
            bw.Write((byte)8); // minimum code size
            var lzw = LiteralLzw(pixels);
            for (int i = 0; i < lzw.Length; i += 255)
            {
                int len = Math.Min(255, lzw.Length - i);
                bw.Write((byte)len);
                bw.Write(lzw, i, len);
            }
            bw.Write((byte)0);
        }
        bw.Write((byte)0x3B);
    }

    /// <summary>LZW stream that emits only literal codes (9 bits) and a clear code every 250 symbols, so the code size never grows.</summary>
    static byte[] LiteralLzw(byte[] pixels)
    {
        var output = new List<byte>();
        int bitBuffer = 0, bitCount = 0;
        void Put(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += 9;
            while (bitCount >= 8) { output.Add((byte)(bitBuffer & 0xFF)); bitBuffer >>= 8; bitCount -= 8; }
        }
        const int Clear = 256, End = 257;
        Put(Clear);
        for (int i = 0; i < pixels.Length; i++)
        {
            Put(pixels[i]);
            if ((i + 1) % 250 == 0) Put(Clear);
        }
        Put(End);
        if (bitCount > 0) output.Add((byte)(bitBuffer & 0xFF));
        return output.ToArray();
    }
}
