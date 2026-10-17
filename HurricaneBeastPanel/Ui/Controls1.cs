using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel.Ui;

public enum ButtonStyle { Filled, Tonal, Outlined, Text }

/// <summary>Pill-shaped Material button with an optional icon glyph.</summary>
public class MButton : Control
{
    bool _hover, _down;

    public ButtonStyle Style { get; set; } = ButtonStyle.Tonal;
    /// <summary>Icon from the icon font (see <see cref="Glyph"/>), drawn before the text.</summary>
    public string Glyph { get; set; } = "";

    public MButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(9.5f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(110, 36);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        float dp = Pal.Dp(this);
        var text = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(Text, Font);
        float icon = string.IsNullOrEmpty(Glyph) ? 0 : 20 * dp + (text.Width > 0 ? 6 * dp : 0);
        float pad = (string.IsNullOrEmpty(Text) ? 0 : 18) * dp + (string.IsNullOrEmpty(Text) ? 0 : 0);
        float w = text.Width + icon + (string.IsNullOrEmpty(Text) ? 0 : pad * 2);
        if (string.IsNullOrEmpty(Text)) w = 36 * dp;
        return new Size((int)Math.Ceiling(w), (int)Math.Round(36 * dp));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = r.Height / 2;

        Color fill, border = Color.Transparent, fore;
        switch (Style)
        {
            case ButtonStyle.Filled:
                fill = _down ? Pal.PrimaryDeep : _hover ? Pal.Mix(Pal.Primary, Color.White, 0.15f) : Pal.Primary;
                fore = Color.FromArgb(255, 28, 20, 78);
                break;
            case ButtonStyle.Outlined:
                fill = _hover ? Pal.Alpha(Pal.Primary, 28) : Color.Transparent;
                border = _hover ? Pal.Primary : Pal.Outline;
                fore = Pal.Text;
                break;
            case ButtonStyle.Text:
                fill = _down ? Pal.Alpha(Pal.Primary, 50) : _hover ? Pal.Alpha(Pal.Primary, 28) : Color.Transparent;
                fore = Pal.Primary;
                break;
            default: // Tonal
                fill = _down ? Pal.Mix(Pal.PrimaryContainer, Pal.Primary, 0.35f) : _hover ? Pal.Mix(Pal.PrimaryContainer, Pal.Primary, 0.2f) : Pal.PrimaryContainer;
                fore = Pal.Text;
                break;
        }
        if (!Enabled)
        {
            fill = Style == ButtonStyle.Filled || Style == ButtonStyle.Tonal ? Pal.Alpha(Pal.SurfaceHi, 200) : Color.Transparent;
            border = Style == ButtonStyle.Outlined ? Pal.Alpha(Pal.Outline, 120) : Color.Transparent;
            fore = Pal.TextFaint;
        }

        if (fill.A > 0) Pal.FillRound(g, r, radius, fill);
        if (border.A > 0) Pal.StrokeRound(g, r, radius, border, Math.Max(1f, dp));
        if (Focused && ShowFocusCues) Pal.StrokeRound(g, RectangleF.Inflate(r, -2 * dp, -2 * dp), radius, Pal.Alpha(Pal.Primary, 160), Math.Max(1f, dp));

        bool hasIcon = !string.IsNullOrEmpty(Glyph), hasText = !string.IsNullOrEmpty(Text);
        float iconW = hasIcon ? 20 * dp : 0, gap = hasIcon && hasText ? 6 * dp : 0;
        float textW = hasText ? TextRenderer.MeasureText(Text, Font).Width : 0;
        float total = iconW + gap + textW, x = (Width - total) / 2;
        if (hasIcon)
        {
            Pal.DrawGlyph(g, Glyph, 11f, fore, new RectangleF(x, 0, iconW, Height));
            x += iconW + gap;
        }
        if (hasText)
            Pal.DrawText(g, Text, Font, fore, new RectangleF(x - 2, 0, textW + 8, Height), StringAlignment.Near, StringAlignment.Center, ellipsis: false);
    }
}

/// <summary>Checkbox with a rounded purple box.</summary>
public class MCheck : Control
{
    bool _checked, _hover;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MCheck()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(9.5f);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(160, 28);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        float dp = Pal.Dp(this);
        var t = TextRenderer.MeasureText(Text, Font);
        return new Size((int)(26 * dp + t.Width + 6 * dp), (int)Math.Max(t.Height + 8 * dp, 28 * dp));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { Focus(); if (Enabled) Checked = !Checked; base.OnClick(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);
    protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space && Enabled) Checked = !Checked; base.OnKeyUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this), s = 18 * dp;
        var box = new RectangleF(1, (Height - s) / 2, s, s);
        if (_checked)
        {
            Pal.FillRound(g, box, 5 * dp, Enabled ? Pal.Primary : Pal.TextFaint);
            using var pen = new Pen(Color.FromArgb(255, 28, 20, 78), 2f * dp) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[] { new PointF(box.X + s * 0.25f, box.Y + s * 0.52f), new PointF(box.X + s * 0.43f, box.Y + s * 0.7f), new PointF(box.X + s * 0.76f, box.Y + s * 0.3f) });
        }
        else
        {
            if (_hover && Enabled) Pal.FillRound(g, box, 5 * dp, Pal.Alpha(Pal.Primary, 30));
            Pal.StrokeRound(g, box, 5 * dp, _hover && Enabled ? Pal.Primary : Pal.TextFaint, 1.6f * dp);
        }
        Pal.DrawText(g, Text, Font, Enabled ? Pal.Text : Pal.TextFaint, new RectangleF(s + 10 * dp, 0, Width - s - 10 * dp, Height));
        if (Focused && ShowFocusCues) Pal.StrokeRound(g, RectangleF.Inflate(box, 3 * dp, 3 * dp), 7 * dp, Pal.Alpha(Pal.Primary, 150), 1f);
    }
}

/// <summary>Material toggle switch with a label on the right.</summary>
public class MSwitch : Control
{
    bool _checked, _hover;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MSwitch()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(9.5f);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(260, 34);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        float dp = Pal.Dp(this);
        var t = TextRenderer.MeasureText(Text, Font);
        return new Size((int)(52 * dp + 12 * dp + t.Width), (int)(34 * dp));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { Focus(); if (Enabled) Checked = !Checked; base.OnClick(e); }
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);
    protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space && Enabled) Checked = !Checked; base.OnKeyUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this), w = 44 * dp, h = 24 * dp;
        var track = new RectangleF(1, (Height - h) / 2, w, h);
        if (_checked)
        {
            Pal.FillRound(g, track, h / 2, Pal.Primary);
            Dot(g, new PointF(track.Right - h / 2, track.Y + h / 2), h * 0.36f, Color.FromArgb(255, 28, 20, 78));
        }
        else
        {
            Pal.FillRound(g, track, h / 2, _hover ? Pal.SurfaceHi : Pal.SurfaceLo);
            Pal.StrokeRound(g, track, h / 2, Pal.TextFaint, 1.6f * dp);
            Dot(g, new PointF(track.X + h / 2, track.Y + h / 2), h * 0.26f, Pal.TextDim);
        }
        Pal.DrawText(g, Text, Font, Enabled ? Pal.Text : Pal.TextFaint, new RectangleF(w + 14 * dp, 0, Width - w - 14 * dp, Height));
    }

    static void Dot(Graphics g, PointF c, float r, Color color)
    {
        using var b = new SolidBrush(color);
        g.FillEllipse(b, c.X - r, c.Y - r, r * 2, r * 2);
    }
}

/// <summary>Horizontal slider with a round thumb and an optional value readout.</summary>
public class MSlider : Control
{
    int _min, _max = 100, _value;
    bool _drag, _hover;

    public event EventHandler? ValueChanged;

    public int Minimum { get => _min; set { _min = value; Invalidate(); } }
    public int Maximum { get => _max; set { _max = Math.Max(value, _min + 1); Invalidate(); } }
    public bool ShowValue { get; set; } = true;
    public string Suffix { get; set; } = "";
    /// <summary>Kept for API compatibility with TrackBar; unused.</summary>
    public int TickFrequency { get; set; }

    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, _min, _max);
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(9f);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(240, 32);
    }

    RectangleF TrackRect()
    {
        float dp = Pal.Dp(this);
        float right = ShowValue ? 52 * dp : 8 * dp;
        return new RectangleF(10 * dp, Height / 2f - 2 * dp, Math.Max(10, Width - 10 * dp - right), 4 * dp);
    }

    int ValueAt(float x)
    {
        var t = TrackRect();
        float f = Math.Clamp((x - t.X) / t.Width, 0f, 1f);
        return (int)Math.Round(_min + f * (_max - _min));
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); _drag = true; Value = ValueAt(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) Value = ValueAt(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Value += e.Delta > 0 ? 1 : -1; }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int step = Math.Max(1, (_max - _min) / 50);
        if (e.KeyCode is Keys.Left or Keys.Down) Value -= step;
        else if (e.KeyCode is Keys.Right or Keys.Up) Value += step;
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var t = TrackRect();
        float f = (_value - _min) / (float)(_max - _min);
        Pal.FillRound(g, t, t.Height / 2, Enabled ? Pal.SurfaceHi : Pal.SurfaceLo);
        var done = new RectangleF(t.X, t.Y, t.Width * f, t.Height);
        if (done.Width > 1) Pal.FillRound(g, done, t.Height / 2, Enabled ? Pal.Primary : Pal.TextFaint);

        float thumb = (_drag || _hover ? 9 : 8) * dp;
        var c = new PointF(t.X + t.Width * f, t.Y + t.Height / 2);
        if (_drag || (_hover && Enabled))
        {
            using var halo = new SolidBrush(Pal.Alpha(Pal.Primary, 48));
            g.FillEllipse(halo, c.X - thumb * 1.9f, c.Y - thumb * 1.9f, thumb * 3.8f, thumb * 3.8f);
        }
        using (var b = new SolidBrush(Enabled ? Pal.Primary : Pal.TextFaint)) g.FillEllipse(b, c.X - thumb, c.Y - thumb, thumb * 2, thumb * 2);
        if (Focused && ShowFocusCues)
            using (var pen = new Pen(Pal.Alpha(Color.White, 170), Math.Max(1f, dp))) g.DrawEllipse(pen, c.X - thumb - 3 * dp, c.Y - thumb - 3 * dp, (thumb + 3 * dp) * 2, (thumb + 3 * dp) * 2);

        if (ShowValue)
            Pal.DrawText(g, _value + Suffix, Font, Pal.TextDim, new RectangleF(Width - 48 * dp, 0, 46 * dp, Height), StringAlignment.Far);
    }
}

/// <summary>Rounded surface panel with an optional title; the container for grouped settings.</summary>
public class Card : Panel
{
    string _title = "";

    public string Title
    {
        get => _title;
        set { _title = value; Padding = TitlePadding(); Invalidate(); }
    }

    /// <summary>Same as <see cref="Title"/>, so code written for a GroupBox keeps working.</summary>
    public override string Text { get => _title; set => Title = value; }

    public float Radius { get; set; } = 16;
    public Color Fill { get; set; } = Pal.Surface;
    public bool DrawBorder { get; set; } = true;

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = Pal.Text;
        Font = Pal.Ui(9.5f);
        Padding = new Padding(16);
    }

    Padding TitlePadding() => string.IsNullOrEmpty(_title) ? new Padding(16) : new Padding(16, 46, 16, 16);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, r, Radius * dp, Fill);
        if (DrawBorder) Pal.StrokeRound(g, r, Radius * dp, Pal.Alpha(Pal.Outline, 150), Math.Max(1f, dp));
        if (!string.IsNullOrEmpty(_title))
            Pal.DrawText(g, _title, Pal.Ui(10.5f, FontStyle.Bold), Pal.Text, new RectangleF(16 * dp, 12 * dp, Width - 32 * dp, 26 * dp));
    }
}

/// <summary>Vertical icon + caption button for the left navigation rail.</summary>
public class RailButton : Control
{
    bool _hover;

    public IconKind Icon { get; set; }
    public bool Selected { get => _selected; set { _selected = value; Invalidate(); } }
    bool _selected;

    public RailButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(76, 72);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);
    protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode is Keys.Enter or Keys.Space) OnClick(EventArgs.Empty); base.OnKeyUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(4 * dp, 3 * dp, Width - 8 * dp, Height - 6 * dp);
        if (_selected) Pal.FillRound(g, r, 14 * dp, Pal.PrimaryContainer);
        else if (_hover) Pal.FillRound(g, r, 14 * dp, Pal.Alpha(Pal.SurfaceHi, 200));
        if (_selected) Pal.StrokeRound(g, r, 14 * dp, Pal.Alpha(Pal.Primary, 190), Math.Max(1.2f, dp));

        Color fore = _selected ? Pal.Text : _hover ? Pal.Text : Pal.TextDim;
        float icon = 26 * dp;
        Icons.Draw(g, Icon, new RectangleF((Width - icon) / 2, r.Y + 10 * dp, icon, icon), _selected ? Pal.Primary : fore, 1.7f);
        Pal.DrawText(g, Text.ToUpperInvariant(), Pal.Ui(7.5f, _selected ? FontStyle.Bold : FontStyle.Regular), fore,
            new RectangleF(r.X, r.Y + 10 * dp + icon + 2 * dp, r.Width, r.Height - icon - 14 * dp), StringAlignment.Center, StringAlignment.Center);
    }
}

/// <summary>Small rounded status label with a coloured dot.</summary>
public class Chip : Control
{
    public Color Dot { get; set; } = Pal.TextFaint;

    public Chip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(9f, FontStyle.Bold);
        Size = new Size(180, 30);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        float dp = Pal.Dp(this);
        var t = TextRenderer.MeasureText(Text, Font);
        return new Size((int)(t.Width + 44 * dp), (int)(30 * dp));
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (AutoSize) { Size = GetPreferredSize(Size.Empty); } Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, r, r.Height / 2, Pal.Alpha(Dot, 36));
        using (var b = new SolidBrush(Dot)) g.FillEllipse(b, 12 * dp, Height / 2f - 4 * dp, 8 * dp, 8 * dp);
        Pal.DrawText(g, Text, Font, Pal.Text, new RectangleF(26 * dp, 0, Width - 30 * dp, Height));
    }
}
