using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HurricaneBeastPanel.Ui;

namespace HurricaneBeastPanel;

/// <summary>Hue / saturation / value conversions for the colour picker.</summary>
static class Hsv
{
    public static Color ToColor(float h, float s, float v, int alpha = 255)
    {
        h = (h % 360 + 360) % 360;
        s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
        float c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (float r, float g, float b) = (int)(h / 60) switch
        {
            0 => (c, x, 0f), 1 => (x, c, 0f), 2 => (0f, c, x), 3 => (0f, x, c), 4 => (x, 0f, c), _ => (c, 0f, x),
        };
        return Color.FromArgb(alpha, (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    public static (float h, float s, float v) From(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        float h = d < 1e-6f ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        return (h, max < 1e-6f ? 0 : d / max, max);
    }
}

/// <summary>A hue ring around a saturation / brightness disc, as in modern colour pickers.</summary>
public sealed class ColorWheel : Control
{
    float _h, _s = 1, _v = 1;
    Bitmap? _ring, _disc;
    int _builtSize;
    float _discHue = -1;
    enum Drag { None, Hue, Disc }
    Drag _drag;

    /// <summary>Raised while the user drags (not when the colour is set from code).</summary>
    public event EventHandler? ColorChanged;

    public float Hue => _h;
    public float Saturation => _s;
    public float Value => _v;

    public ColorWheel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(300, 300);
        Cursor = Cursors.Hand;
    }

    public void SetHsv(float h, float s, float v)
    {
        _h = (h % 360 + 360) % 360; _s = Math.Clamp(s, 0, 1); _v = Math.Clamp(v, 0, 1);
        Invalidate();
    }

    // ---- geometry ----
    float Size2 => Math.Min(Width, Height);
    PointF Ctr => new(Width / 2f, Height / 2f);
    float OuterR => Size2 / 2f - 3;
    float RingW => OuterR * 0.2f;
    float InnerR => OuterR - RingW - OuterR * 0.07f;
    float Half => InnerR * 0.6f; // the disc maps a square of this half-size, so every S/V pair is reachable

    PointF DiscPoint() => new(Ctr.X + (_s - 0.5f) * 2 * Half, Ctr.Y - (_v - 0.5f) * 2 * Half);

    PointF HuePoint()
    {
        double a = _h * Math.PI / 180, r = OuterR - RingW / 2;
        return new PointF(Ctr.X + (float)(Math.Cos(a) * r), Ctr.Y + (float)(Math.Sin(a) * r));
    }

    // ---- bitmaps ----
    static Bitmap MakeBitmap(int w, int h, Func<int, int, (int a, Color c)> pixel)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[Math.Abs(data.Stride) * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var (a, c) = pixel(x, y);
                int i = y * data.Stride + x * 4;
                bytes[i] = c.B; bytes[i + 1] = c.G; bytes[i + 2] = c.R; bytes[i + 3] = (byte)a;
            }
        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    void EnsureBitmaps()
    {
        int size = (int)Math.Ceiling(Size2);
        if (_ring == null || _builtSize != size)
        {
            _ring?.Dispose(); _disc?.Dispose(); _disc = null;
            _builtSize = size;
            float cx = Width / 2f, cy = Height / 2f, r1 = OuterR, r0 = OuterR - RingW;
            _ring = MakeBitmap(Width, Height, (x, y) =>
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
                float cover = Math.Clamp(Math.Min(d - r0 + 0.5f, r1 - d + 0.5f), 0, 1);
                if (cover <= 0) return (0, Color.Black);
                float hue = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                return ((int)(cover * 255), Hsv.ToColor(hue, 1, 1));
            });
        }
        if (_disc == null || Math.Abs(_discHue - _h) > 0.001f)
        {
            _disc?.Dispose();
            _discHue = _h;
            float cx = Width / 2f, cy = Height / 2f, r = InnerR, half = Half, hue = _h;
            _disc = MakeBitmap(Width, Height, (x, y) =>
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d = MathF.Sqrt(dx * dx + dy * dy);
                float cover = Math.Clamp(r - d + 0.5f, 0, 1);
                if (cover <= 0) return (0, Color.Black);
                float s = Math.Clamp(0.5f + dx / (2 * half), 0, 1), v = Math.Clamp(0.5f - dy / (2 * half), 0, 1);
                return ((int)(cover * 255), Hsv.ToColor(hue, s, v));
            });
        }
    }

    // ---- mouse ----
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        float dx = e.X - Ctr.X, dy = e.Y - Ctr.Y, d = MathF.Sqrt(dx * dx + dy * dy);
        float split = OuterR - RingW - OuterR * 0.035f;
        if (d > OuterR + 6) return;
        _drag = d >= split ? Drag.Hue : Drag.Disc;
        Capture = true;
        Apply(e.X, e.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag != Drag.None) Apply(e.X, e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _drag = Drag.None;
        Capture = false;
    }

    void Apply(float x, float y)
    {
        float dx = x - Ctr.X, dy = y - Ctr.Y;
        if (_drag == Drag.Hue)
        {
            _h = (MathF.Atan2(dy, dx) * 180f / MathF.PI + 360) % 360;
        }
        else
        {
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > InnerR) { dx *= InnerR / d; dy *= InnerR / d; }
            _s = Math.Clamp(0.5f + dx / (2 * Half), 0, 1);
            _v = Math.Clamp(0.5f - dy / (2 * Half), 0, 1);
        }
        Invalidate();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- painting ----
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        EnsureBitmaps();
        g.DrawImageUnscaled(_ring!, 0, 0);
        g.DrawImageUnscaled(_disc!, 0, 0);
        float dp = Pal.Dp(this);

        // Soft outline around the disc so it separates from dark backgrounds.
        using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), Math.Max(1f, dp)))
            g.DrawEllipse(pen, Ctr.X - InnerR, Ctr.Y - InnerR, InnerR * 2, InnerR * 2);

        Knob(g, HuePoint(), RingW * 0.46f, Hsv.ToColor(_h, 1, 1), dp);
        Knob(g, DiscPoint(), RingW * 0.4f, Hsv.ToColor(_h, _s, _v), dp);
    }

    static void Knob(Graphics g, PointF p, float r, Color fill, float dp)
    {
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            g.FillEllipse(shadow, p.X - r - dp, p.Y - r + dp, (r + dp) * 2, (r + dp) * 2);
        using (var ring = new SolidBrush(Color.White)) g.FillEllipse(ring, p.X - r, p.Y - r, r * 2, r * 2);
        float inner = r - Math.Max(2f, 2.5f * dp);
        using (var b = new SolidBrush(fill)) g.FillEllipse(b, p.X - inner, p.Y - inner, inner * 2, inner * 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _ring?.Dispose(); _disc?.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Opacity slider drawn over a checkerboard, fading from clear to the chosen colour.</summary>
public sealed class AlphaBar : Control
{
    Color _color = Color.White;
    int _alpha = 255;
    bool _drag;

    public event EventHandler? AlphaChanged;

    public int Alpha
    {
        get => _alpha;
        set { _alpha = Math.Clamp(value, 0, 255); Invalidate(); }
    }

    public Color BaseColor
    {
        get => _color;
        set { _color = Color.FromArgb(255, value); Invalidate(); }
    }

    public AlphaBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(400, 28);
        Cursor = Cursors.Hand;
    }

    RectangleF Track
    {
        get
        {
            float pad = Height / 2f;
            float h = Height * 0.5f;
            return new RectangleF(pad, (Height - h) / 2, Width - pad * 2, h);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { _drag = true; Capture = true; Set(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (_drag) Set(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _drag = false; Capture = false; }

    void Set(float x)
    {
        var t = Track;
        _alpha = (int)Math.Round(Math.Clamp((x - t.X) / t.Width, 0f, 1f) * 255);
        Invalidate();
        AlphaChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        var t = Track;
        using var path = Pal.Round(t, t.Height / 2);
        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        UiText.Checker(g, t, Math.Max(4, (int)(t.Height / 2)));
        using (var grad = new LinearGradientBrush(t, Color.FromArgb(0, _color), Color.FromArgb(255, _color), 0f))
            g.FillRectangle(grad, t);
        g.Restore(state);

        float x = t.X + t.Width * _alpha / 255f, r = Height / 2f - 2;
        float dp = Pal.Dp(this);
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(shadow, x - r, Height / 2f - r + dp, r * 2, r * 2);
        using (var ring = new SolidBrush(Color.White)) g.FillEllipse(ring, x - r, Height / 2f - r, r * 2, r * 2);
        float inner = r - Math.Max(2f, 2.5f * dp);
        using (var b = new SolidBrush(Color.FromArgb(_alpha, _color))) { UiText.Checker(g, new RectangleF(x - inner, Height / 2f - inner, inner * 2, inner * 2), 4); g.FillEllipse(b, x - inner, Height / 2f - inner, inner * 2, inner * 2); }
    }
}

/// <summary>Small pipette button that lets the user pick any colour on the screen.</summary>
public sealed class EyedropperButton : Control
{
    bool _hover;

    public EyedropperButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(40, 40);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (_hover) Pal.FillRound(g, r, 10 * dp, Pal.Alpha(Pal.Primary, 40));
        float u = Math.Min(Width, Height) / 24f;
        using var pen = new Pen(_hover ? Pal.Primary : Pal.TextDim, 1.7f * Math.Max(dp, 0.8f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        // A pipette drawn upright (rubber bulb, collar, glass tube, tip), then turned so the tip points down-left.
        var state = g.Save();
        g.TranslateTransform(Width / 2f, Height / 2f);
        g.RotateTransform(45);
        g.ScaleTransform(u, u);
        pen.Width /= u;
        using (var bulb = Pal.Round(new RectangleF(-2.8f, -11.5f, 5.6f, 6.5f), 2.6f)) g.DrawPath(pen, bulb);
        g.DrawLine(pen, -5f, -5f, 5f, -5f);                               // collar
        g.DrawLines(pen, new[] { new PointF(-2f, -5f), new PointF(-2f, 5.5f), new PointF(0f, 10f), new PointF(2f, 5.5f), new PointF(2f, -5f) });
        g.DrawLine(pen, -2f, 0f, 2f, 0f);                                  // level mark on the tube
        g.Restore(state);
    }
}
/// <summary>Full-screen overlay showing a snapshot of the desktop; click to pick the colour under the cursor, Esc to cancel.</summary>
sealed class ScreenPickerForm : Form
{
    readonly Bitmap _shot;
    Point _mouse;

    public Color Result { get; private set; }

    public ScreenPickerForm()
    {
        var bounds = SystemInformation.VirtualScreen;
        _shot = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(_shot)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
    }

    protected override void OnMouseMove(MouseEventArgs e) { _mouse = e.Location; Invalidate(); base.OnMouseMove(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.X >= 0 && e.Y >= 0 && e.X < _shot.Width && e.Y < _shot.Height)
        {
            Result = _shot.GetPixel(e.X, e.Y);
            DialogResult = DialogResult.OK;
        }
        else DialogResult = DialogResult.Cancel;
        base.OnMouseDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.DrawImageUnscaled(_shot, 0, 0);
        if (_mouse.X < 0 || _mouse.Y < 0 || _mouse.X >= _shot.Width || _mouse.Y >= _shot.Height) return;

        // Magnifier with the colour under the cursor, drawn beside it.
        const int cells = 11, cell = 12;
        int size = cells * cell;
        var box = new Rectangle(_mouse.X + 24, _mouse.Y + 24, size, size + 28);
        if (box.Right > Width) box.X = _mouse.X - 24 - box.Width;
        if (box.Bottom > Height) box.Y = _mouse.Y - 24 - box.Height;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.FillRectangle(Brushes.Black, Rectangle.Inflate(box, 2, 2));
        g.DrawImage(_shot, new Rectangle(box.X, box.Y, size, size),
            _mouse.X - cells / 2, _mouse.Y - cells / 2, cells, cells, GraphicsUnit.Pixel);
        g.DrawRectangle(Pens.White, box.X + size / 2 - cell / 2, box.Y + size / 2 - cell / 2, cell, cell);
        var c = _shot.GetPixel(_mouse.X, _mouse.Y);
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        g.DrawString($"#{c.R:X2}{c.G:X2}{c.B:X2}", font, Brushes.White, box.X + 4, box.Y + size + 4);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _shot.Dispose();
        base.Dispose(disposing);
    }
}
