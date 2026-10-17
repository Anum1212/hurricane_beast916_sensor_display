using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace HurricaneBeastPanel.Ui;

/// <summary>Colours, fonts and drawing helpers for the dark purple Material-style look.</summary>
public static class Pal
{
    static Color H(string hex) => ColorUtil.Parse(hex);

    public static readonly Color Bg = H("#FF14112B");
    public static readonly Color Surface = H("#FF1F1B3F");
    public static readonly Color SurfaceHi = H("#FF2A2554");
    public static readonly Color SurfaceLo = H("#FF181533");
    public static readonly Color Outline = H("#FF3B3570");
    public static readonly Color Primary = H("#FF8B7BFF");
    public static readonly Color PrimaryDeep = H("#FF6C5CE7");
    public static readonly Color PrimaryContainer = H("#FF352E7A");
    public static readonly Color Text = H("#FFEEEBFF");
    public static readonly Color TextDim = H("#FFA5A1C9");
    public static readonly Color TextFaint = H("#FF6F6B99");
    public static readonly Color Coral = H("#FFFF7A59");
    public static readonly Color Green = H("#FF5EE6A8");
    public static readonly Color Amber = H("#FFFFC857");
    public static readonly Color Red = H("#FFFF6B81");

    /// <summary>Pixels per design unit on the control's monitor.</summary>
    public static float Dp(Control c) => c.DeviceDpi / 96f;

    public static Color Alpha(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c.R, c.G, c.B);

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    // ---------- fonts ----------

    static readonly Dictionary<(float, FontStyle), Font> FontCache = new();

    /// <summary>Shared UI font (points). Do not dispose.</summary>
    public static Font Ui(float points = 9.5f, FontStyle style = FontStyle.Regular)
    {
        lock (FontCache)
        {
            if (!FontCache.TryGetValue((points, style), out var f))
                FontCache[(points, style)] = f = new Font(UiFamily, points, style, GraphicsUnit.Point);
            return f;
        }
    }

    static readonly Lazy<string> UiFamilyName = new(() => Installed("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI");
    static string UiFamily => UiFamilyName.Value;

    static readonly Lazy<string> IconFamilyName = new(() =>
        Installed("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets");

    static bool Installed(string family)
    {
        using var fonts = new InstalledFontCollection();
        return fonts.Families.Any(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase));
    }

    static readonly Dictionary<float, Font> IconCache = new();

    /// <summary>Icon glyph font (Segoe Fluent Icons / MDL2 Assets). Do not dispose.</summary>
    public static Font Icon(float points = 11f)
    {
        lock (IconCache)
        {
            if (!IconCache.TryGetValue(points, out var f))
                IconCache[points] = f = new Font(IconFamilyName.Value, points, GraphicsUnit.Point);
            return f;
        }
    }

    // ---------- drawing ----------

    public static void Quality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, RectangleF r, float radius, Color color)
    {
        using var path = Round(r, radius);
        using var b = new SolidBrush(color);
        g.FillPath(b, path);
    }

    public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width = 1f)
    {
        using var path = Round(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void DrawText(Graphics g, string text, Font font, Color color, RectangleF r,
        StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center, bool ellipsis = true)
    {
        using var b = new SolidBrush(color);
        using var fmt = new StringFormat
        {
            Alignment = h, LineAlignment = v, FormatFlags = StringFormatFlags.NoWrap,
            Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None,
        };
        g.DrawString(text, font, b, r, fmt);
    }

    public static void DrawGlyph(Graphics g, string glyph, float points, Color color, RectangleF r)
    {
        using var b = new SolidBrush(color);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.DrawString(glyph, Icon(points), b, r, fmt);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    // ---------- window chrome ----------

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Dark title bar in the app colours with rounded corners (Windows 11; harmlessly ignored on older systems).</summary>
    public static void DarkChrome(Form form)
    {
        try
        {
            IntPtr h = form.Handle;
            int on = 1, round = 2, caption = ColorRef(Bg), border = ColorRef(Outline), text = ColorRef(Text);
            DwmSetWindowAttribute(h, 20, ref on, 4);        // immersive dark mode
            DwmSetWindowAttribute(h, 33, ref round, 4);     // rounded corners
            DwmSetWindowAttribute(h, 34, ref border, 4);    // border colour
            DwmSetWindowAttribute(h, 35, ref caption, 4);   // caption colour
            DwmSetWindowAttribute(h, 36, ref text, 4);      // caption text colour
        }
        catch { /* older Windows */ }
    }

    /// <summary>Gives native scrollbars the dark look.</summary>
    public static void DarkScroll(Control c)
    {
        void Apply() { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        if (c.IsHandleCreated) Apply(); else c.HandleCreated += (_, _) => Apply();
    }

    /// <summary>Turns on double buffering for plain panels.</summary>
    public static void Buffered(Control c)
    {
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(c, true);
    }
}

/// <summary>Base form with the dark purple chrome and default colours.</summary>
public class UiForm : Form
{
    public UiForm()
    {
        BackColor = Pal.Bg;
        ForeColor = Pal.Text;
        Font = Pal.Ui(9.5f);
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Pal.DarkChrome(this);
    }
}

public enum IconKind { Gauge, Graph, Meter, Monitor, Text, Shape, Frame, Plate, Overlay, Custom, Home, Themes, Settings, Device, Weather }

/// <summary>Outline category icons drawn as vectors (so they stay sharp at any display scale).</summary>
public static class Icons
{
    public static void Draw(Graphics g, IconKind kind, RectangleF r, Color color, float stroke = 1.8f)
    {
        float s = Math.Min(r.Width, r.Height);
        var box = new RectangleF(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
        float u = s / 24f; // design on a 24 unit grid
        PointF P(float x, float y) => new(box.X + x * u, box.Y + y * u);
        using var pen = new Pen(color, stroke * Math.Max(u, 0.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var oldSmooth = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (kind)
        {
            case IconKind.Gauge:
                g.DrawArc(pen, box.X + 3 * u, box.Y + 5 * u, 18 * u, 18 * u, 180, 180);
                g.DrawLine(pen, P(12, 14), P(16, 9));
                g.DrawLine(pen, P(5, 19), P(6.5f, 19));
                g.DrawLine(pen, P(17.5f, 19), P(19, 19));
                break;
            case IconKind.Graph:
                g.DrawLines(pen, new[] { P(3, 17), P(8, 11), P(12, 14), P(17, 6), P(21, 10) });
                break;
            case IconKind.Meter:
                using (var path = Pal.Round(new RectangleF(box.X + 3 * u, box.Y + 8 * u, 18 * u, 8 * u), 3 * u)) g.DrawPath(pen, path);
                g.DrawLine(pen, P(6, 12), P(12, 12));
                break;
            case IconKind.Monitor:
                using (var path = Pal.Round(new RectangleF(box.X + 3 * u, box.Y + 5 * u, 18 * u, 12 * u), 2.5f * u)) g.DrawPath(pen, path);
                g.DrawLine(pen, P(8, 21), P(16, 21));
                g.DrawLine(pen, P(12, 17), P(12, 21));
                g.DrawLine(pen, P(7, 11), P(9, 11));
                g.DrawLine(pen, P(11, 11), P(13, 11));
                break;
            case IconKind.Text:
                g.DrawLine(pen, P(5, 6), P(19, 6));
                g.DrawLine(pen, P(12, 6), P(12, 19));
                break;
            case IconKind.Shape:
                using (var path = Pal.Round(new RectangleF(box.X + 5 * u, box.Y + 5 * u, 14 * u, 14 * u), 2 * u)) g.DrawPath(pen, path);
                break;
            case IconKind.Frame:
                foreach (var (a, b, c) in new[] { (P(4, 9), P(4, 4), P(9, 4)), (P(15, 4), P(20, 4), P(20, 9)), (P(4, 15), P(4, 20), P(9, 20)), (P(15, 20), P(20, 20), P(20, 15)) })
                    g.DrawLines(pen, new[] { a, b, c });
                break;
            case IconKind.Plate:
                using (var path = Pal.Round(new RectangleF(box.X + 3 * u, box.Y + 6 * u, 18 * u, 12 * u), 3 * u)) g.DrawPath(pen, path);
                g.DrawEllipse(pen, box.X + 7 * u, box.Y + 9 * u, 4 * u, 4 * u);
                g.DrawEllipse(pen, box.X + 13 * u, box.Y + 9 * u, 4 * u, 4 * u);
                break;
            case IconKind.Overlay:
                g.DrawEllipse(pen, box.X + 5 * u, box.Y + 5 * u, 14 * u, 14 * u);
                g.DrawLine(pen, P(12, 2), P(12, 8));
                g.DrawLine(pen, P(12, 16), P(12, 22));
                g.DrawLine(pen, P(2, 12), P(8, 12));
                g.DrawLine(pen, P(16, 12), P(22, 12));
                break;
            case IconKind.Custom:
                using (var path = Pal.Round(new RectangleF(box.X + 3 * u, box.Y + 4 * u, 18 * u, 16 * u), 3 * u)) g.DrawPath(pen, path);
                g.DrawLines(pen, new[] { P(5, 17), P(10, 12), P(14, 16), P(17, 13), P(20, 16) });
                g.DrawEllipse(pen, box.X + 14 * u, box.Y + 7 * u, 3 * u, 3 * u);
                break;
            case IconKind.Home:
                g.DrawLines(pen, new[] { P(4, 11), P(12, 4), P(20, 11) });
                g.DrawLines(pen, new[] { P(6, 10), P(6, 20), P(18, 20), P(18, 10) });
                g.DrawLine(pen, P(10, 20), P(10, 14));
                g.DrawLine(pen, P(10, 14), P(14, 14));
                g.DrawLine(pen, P(14, 14), P(14, 20));
                break;
            case IconKind.Themes:
                foreach (var (x, y) in new[] { (4f, 4f), (14f, 4f), (4f, 14f), (14f, 14f) })
                    using (var path = Pal.Round(new RectangleF(box.X + x * u, box.Y + y * u, 6 * u, 6 * u), 1.6f * u)) g.DrawPath(pen, path);
                break;
            case IconKind.Settings:
                g.DrawEllipse(pen, box.X + 8 * u, box.Y + 8 * u, 8 * u, 8 * u);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(pen, P(12 + (float)Math.Cos(a) * 7.5f, 12 + (float)Math.Sin(a) * 7.5f), P(12 + (float)Math.Cos(a) * 10, 12 + (float)Math.Sin(a) * 10));
                }
                break;
            case IconKind.Weather:
                g.DrawEllipse(pen, box.X + 12 * u, box.Y + 3 * u, 7 * u, 7 * u);
                g.DrawLine(pen, P(15.5f, 1), P(15.5f, 1.8f));
                g.DrawLine(pen, P(21.5f, 6.5f), P(22.4f, 6.5f));
                g.DrawLine(pen, P(20, 2), P(20.6f, 1.4f));
                g.DrawArc(pen, box.X + 3 * u, box.Y + 11 * u, 7 * u, 7 * u, 90, 180);
                g.DrawArc(pen, box.X + 6 * u, box.Y + 8 * u, 9 * u, 10 * u, 180, 180);
                g.DrawArc(pen, box.X + 12 * u, box.Y + 11 * u, 8 * u, 7 * u, 270, 180);
                g.DrawLine(pen, P(6.5f, 18), P(16, 18));
                break;
            case IconKind.Device:
                using (var path = Pal.Round(new RectangleF(box.X + 2 * u, box.Y + 8 * u, 20 * u, 8 * u), 2 * u)) g.DrawPath(pen, path);
                g.DrawLine(pen, P(6, 12), P(10, 12));
                g.DrawLine(pen, P(8, 20), P(16, 20));
                break;
        }
        g.SmoothingMode = oldSmooth;
    }
}

/// <summary>Segoe Fluent / MDL2 glyph code points used for buttons.</summary>
public static class Glyph
{
    public const string Add = "", Edit = "", Delete = "", Save = "", Undo = "", Redo = "",
        Search = "", Copy = "", Eye = "", EyeOff = "", ChevronDown = "", ChevronUp = "",
        Up = "", Down = "", Rotate = "", Play = "", Stop = "", Brightness = "",
        Folder = "", Image = "", Back = "", ZoomIn = "", ZoomOut = "", Fit = "",
        Check = "", Close = "", Cancel = "", More = "", Refresh = "", Layers = "",
        Pin = "", Info = "";
}
