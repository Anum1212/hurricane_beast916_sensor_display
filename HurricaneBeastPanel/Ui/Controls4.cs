using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HurricaneBeastPanel.Ui;

/// <summary>Shows the latest frame sent to the screen, fitted and rounded, on a black "screen".</summary>
public class PreviewBox : Control
{
    Bitmap? _image;

    public string Placeholder { get; set; } = "Waiting for the first frame...";

    public Bitmap? Image
    {
        get => _image;
        set
        {
            var old = _image;
            _image = value;
            old?.Dispose();
            Invalidate();
        }
    }

    public PreviewBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(9.5f);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var outer = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, outer, 16 * dp, Color.Black);
        if (_image == null)
        {
            Pal.DrawText(g, Placeholder, Font, Pal.TextFaint, outer, StringAlignment.Center);
        }
        else
        {
            float pad = 8 * dp;
            var area = RectangleF.Inflate(outer, -pad, -pad);
            float scale = Math.Min(area.Width / _image.Width, area.Height / _image.Height);
            float w = _image.Width * scale, h = _image.Height * scale;
            var dest = new RectangleF(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
            using var path = Pal.Round(dest, 10 * dp);
            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            g.DrawImage(_image, dest);
            g.Restore(state);
        }
        Pal.StrokeRound(g, outer, 16 * dp, Pal.Alpha(Pal.Outline, 200), Math.Max(1f, dp));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _image?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A label above a large value, used for the live sensor readings.</summary>
public class StatTile : Control
{
    string _value = "--";

    public string Caption { get; set; } = "";
    public Color Accent { get; set; } = Pal.Primary;

    public string Value
    {
        get => _value;
        set { if (_value == value) return; _value = value; Invalidate(); }
    }

    public StatTile()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(150, 84);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, r, 14 * dp, Pal.SurfaceLo);
        Pal.StrokeRound(g, r, 14 * dp, Pal.Alpha(Pal.Outline, 120), Math.Max(1f, dp));
        using (var b = new SolidBrush(Accent)) g.FillEllipse(b, 14 * dp, 16 * dp, 7 * dp, 7 * dp);
        Pal.DrawText(g, Caption.ToUpperInvariant(), Pal.Ui(8f, FontStyle.Bold), Pal.TextDim, new RectangleF(28 * dp, 8 * dp, Width - 36 * dp, 24 * dp));
        Pal.DrawText(g, _value, Pal.Ui(17f, FontStyle.Bold), Pal.Text, new RectangleF(14 * dp, 32 * dp, Width - 24 * dp, Height - 38 * dp));
    }
}

/// <summary>A name on the left and a value on the right, with a hairline underneath.</summary>
public class InfoRow : Control
{
    string _value = "-";

    public string Caption { get; set; } = "";

    public string Value
    {
        get => _value;
        set { if (_value == value) return; _value = value; Invalidate(); }
    }

    public InfoRow()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(300, 34);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        Pal.DrawText(g, Caption, Pal.Ui(9.5f), Pal.TextDim, new RectangleF(0, 0, Width * 0.38f, Height));
        Pal.DrawText(g, _value, Pal.Ui(9.5f, FontStyle.Bold), Pal.Text, new RectangleF(Width * 0.38f, 0, Width * 0.62f, Height), StringAlignment.Far);
        using var pen = new Pen(Pal.Alpha(Pal.Outline, 90), Math.Max(1f, dp));
        g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}

/// <summary>Generated application icon (a purple gauge), used for windows, the tray and the exe.</summary>
public static class AppIcon
{
    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = size;
        using (var path = Pal.Round(new RectangleF(s * 0.04f, s * 0.04f, s * 0.92f, s * 0.92f), s * 0.24f))
        using (var bg = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(255, 139, 123, 255), Color.FromArgb(255, 82, 62, 214), 55f))
            g.FillPath(bg, path);
        float pw = Math.Max(2f, s * 0.085f);
        using (var track = new Pen(Color.FromArgb(90, 255, 255, 255), pw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(track, s * 0.22f, s * 0.22f, s * 0.56f, s * 0.56f, 135, 270);
        using (var arc = new Pen(Color.White, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(arc, s * 0.22f, s * 0.22f, s * 0.56f, s * 0.56f, 135, 190);
        using (var needle = new Pen(Color.White, Math.Max(1.5f, s * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(needle, s * 0.5f, s * 0.5f, s * 0.68f, s * 0.36f);
        using var hub = new SolidBrush(Color.White);
        g.FillEllipse(hub, s * 0.455f, s * 0.455f, s * 0.09f, s * 0.09f);
        return bmp;
    }

    static Icon? _cached;

    /// <summary>Icon for forms and the tray (do not dispose).</summary>
    public static Icon Small()
    {
        if (_cached != null) return _cached;
        using var bmp = Render(64);
        var handle = bmp.GetHicon();
        using var temp = Icon.FromHandle(handle);
        _cached = (Icon)temp.Clone();
        return _cached;
    }

    /// <summary>Writes a Windows .ico (256 px PNG inside) for the exe icon.</summary>
    public static void WriteIco(string path)
    {
        using var bmp = Render(256);
        using var png = new MemoryStream();
        bmp.Save(png, ImageFormat.Png);
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write((short)0); w.Write((short)1); w.Write((short)1);       // ICONDIR: reserved, type icon, 1 image
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // 256x256, no palette
        w.Write((short)1); w.Write((short)32);                          // planes, bits
        w.Write((int)png.Length); w.Write(22);                          // size, offset
        w.Write(png.ToArray());
    }
}
