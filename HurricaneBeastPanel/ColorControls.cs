using System.Drawing.Drawing2D;
using HurricaneBeastPanel.Ui;

namespace HurricaneBeastPanel;

/// <summary>Small helpers for building labels in the dark purple style.</summary>
static class UiText
{
    public static Label Label(string text, bool dim = true, float points = 9f) => new()
    {
        Text = text, AutoSize = true, BackColor = Color.Transparent,
        ForeColor = dim ? Pal.TextDim : Pal.Text, Font = Pal.Ui(points),
        Margin = new Padding(0, 8, 8, 0),
    };

    public static void Checker(Graphics g, RectangleF r, int cell = 7)
    {
        using (var light = new SolidBrush(Color.FromArgb(255, 70, 66, 100))) g.FillRectangle(light, r);
        using var dark = new SolidBrush(Color.FromArgb(255, 48, 45, 78));
        for (float y = 0; y < r.Height; y += cell)
            for (float x = (int)(y / cell) % 2 * cell; x < r.Width; x += cell * 2)
                g.FillRectangle(dark, r.X + x, r.Y + y, Math.Min(cell, r.Width - x), Math.Min(cell, r.Height - y));
    }
}

/// <summary>Grid of colour swatches painted directly (used for the picker presets).</summary>
sealed class SwatchGrid : Control
{
    readonly string[] _colors;
    int _hover = -1;

    public event Action<Color>? Picked;

    public SwatchGrid(string[] colors)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        _colors = colors;
        Cursor = Cursors.Hand;
        Size = new Size(380, 70);
    }

    const int Columns = 12;

    RectangleF Cell(int i)
    {
        float dp = Pal.Dp(this), gap = 6 * dp;
        float size = (Width - gap * (Columns - 1)) / Columns;
        return new RectangleF(i % Columns * (size + gap), i / Columns * (size + gap), size, size);
    }

    int IndexAt(Point p)
    {
        for (int i = 0; i < _colors.Length; i++) if (Cell(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { int i = IndexAt(e.Location); if (i != _hover) { _hover = i; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { int i = IndexAt(e.Location); if (i >= 0) Picked?.Invoke(ColorUtil.Parse(_colors[i])); base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        for (int i = 0; i < _colors.Length; i++)
        {
            var r = Cell(i);
            using (var path = Pal.Round(r, 7 * dp))
            {
                var state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                UiText.Checker(g, r, 5);
                using (var b = new SolidBrush(ColorUtil.Parse(_colors[i]))) g.FillRectangle(b, r);
                g.Restore(state);
            }
            Pal.StrokeRound(g, r, 7 * dp, i == _hover ? Pal.Primary : Pal.Alpha(Pal.Outline, 200), i == _hover ? 2f * dp : Math.Max(1f, dp));
        }
    }
}

/// <summary>Colour picker with R/G/B/A sliders, hex entry and preset swatches. Reports changes live while open.</summary>
public sealed class ColorPickerDialog : UiForm
{
    static readonly string[] Presets =
    {
        "#FFFFFFFF", "#FFC8CDD8", "#FF8C97AC", "#FF2E3548", "#FF181C28", "#FF0C0E14", "#FF000000", "#00000000", "#FF8B7BFF", "#FF6C5CE7", "#FF352E7A", "#FF1F1B3F",
        "#FFFF5A5A", "#FFFF825A", "#FFFFD25A", "#FF46DC8C", "#FF46DCDC", "#FF5AAAFF", "#FF3A7BFF", "#FFAF87FF", "#FFFF6EA8", "#FFE63946", "#FF2A9D8F", "#FF264653",
        "#FFF4A261", "#FF8338EC", "#FF06D6A0", "#FFFFBE0B", "#FF00D2D2", "#FF2BF28A", "#FFFF9F1C", "#FFB5179E", "#FF4361EE", "#FF4CC9F0", "#FFF72585", "#FF7209B7",
    };

    readonly MSlider _r = Slider(), _g = Slider(), _b = Slider(), _a = Slider();
    readonly MTextBox _hex = new() { Width = 120, Height = 36 };
    readonly Panel _swatch = new() { Width = 120, Height = 56 };
    readonly Action<Color>? _live;
    bool _updating;

    public Color SelectedColor { get; private set; }

    static MSlider Slider() => new() { Minimum = 0, Maximum = 255, Width = 330, Height = 30, ShowValue = true };

    ColorPickerDialog(Color initial, Action<Color>? live)
    {
        _live = live;
        SelectedColor = initial;
        Text = "Pick a color";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 470);

        _swatch.Location = new Point(20, 20);
        _swatch.Paint += (_, e) =>
        {
            var g = e.Graphics;
            Pal.Quality(g);
            var r = new RectangleF(0, 0, _swatch.Width - 1, _swatch.Height - 1);
            using var path = Pal.Round(r, 12 * Pal.Dp(this));
            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            UiText.Checker(g, r);
            using (var b = new SolidBrush(SelectedColor)) g.FillRectangle(b, r);
            g.Restore(state);
            using var pen = new Pen(Pal.Outline);
            g.DrawPath(pen, path);
        };
        Controls.Add(_swatch);

        var hexLabel = UiText.Label("HEX", points: 8.5f);
        hexLabel.Location = new Point(160, 22);
        Controls.Add(hexLabel);
        _hex.Location = new Point(160, 44);
        Controls.Add(_hex);

        string[] names = { "R", "G", "B", "A" };
        var sliders = new[] { _r, _g, _b, _a };
        for (int i = 0; i < 4; i++)
        {
            var l = UiText.Label(names[i], dim: false);
            l.Location = new Point(22, 98 + i * 40 + 3);
            sliders[i].Location = new Point(48, 98 + i * 40);
            Controls.Add(l);
            Controls.Add(sliders[i]);
            sliders[i].ValueChanged += (_, _) => { if (!_updating) SetColor(Color.FromArgb(_a.Value, _r.Value, _g.Value, _b.Value)); };
        }

        var presetsLabel = UiText.Label("PRESETS", points: 8.5f);
        presetsLabel.Location = new Point(22, 266);
        Controls.Add(presetsLabel);
        var grid = new SwatchGrid(Presets) { Location = new Point(20, 292), Size = new Size(400, 100) };
        grid.Picked += c => SetColor(c);
        Controls.Add(grid);

        var ok = new MButton { Text = "OK", Style = ButtonStyle.Filled, Location = new Point(236, 416), Size = new Size(90, 38) };
        var cancel = new MButton { Text = "Cancel", Style = ButtonStyle.Text, Location = new Point(334, 416), Size = new Size(86, 38) };
        Controls.Add(ok);
        Controls.Add(cancel);
        ok.Click += (_, _) => DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        _hex.TextChanged += (_, _) =>
        {
            if (_updating) return;
            string h = _hex.Text.Trim().TrimStart('#');
            if (h.Length is 6 or 8) SetColor(ColorUtil.Parse(h), fromHex: true);
        };
        SetColor(initial);
        DpiHelper.Fit(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void SetColor(Color c, bool fromHex = false)
    {
        _updating = true;
        SelectedColor = c;
        _r.Value = c.R; _g.Value = c.G; _b.Value = c.B; _a.Value = c.A;
        if (!fromHex) _hex.Text = ColorUtil.ToHex(c);
        _swatch.Invalidate();
        _updating = false;
        _live?.Invoke(c);
    }

    /// <summary>Shows the dialog. Returns the chosen colour, or null if cancelled (the live callback is then reset to the original).</summary>
    public static Color? Pick(IWin32Window owner, Color initial, Action<Color>? live = null)
    {
        using var dlg = new ColorPickerDialog(initial, live);
        if (dlg.ShowDialog(owner) == DialogResult.OK) return dlg.SelectedColor;
        live?.Invoke(initial);
        return null;
    }
}

/// <summary>Rounded swatch button showing the colour and its hex code; click opens the picker.</summary>
public sealed class ColorButton : Control
{
    string _hex = "#FFFFFFFF";
    bool _hover;

    public event EventHandler? ColorChanged;

    public string Hex
    {
        get => _hex;
        set { _hex = value; Invalidate(); }
    }

    public ColorButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Pal.Ui(8.5f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(130, 34);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (!Enabled) return;
        string original = _hex;
        var picked = ColorPickerDialog.Pick(FindForm()!, ColorUtil.Parse(_hex), c =>
        {
            _hex = ColorUtil.ToHex(c);
            Invalidate();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        });
        if (picked == null)
        {
            _hex = original;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var c = ColorUtil.Parse(_hex);
        using (var path = Pal.Round(r, 10 * dp))
        {
            var state = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            UiText.Checker(g, r);
            using (var b = new SolidBrush(c)) g.FillRectangle(b, r);
            g.Restore(state);
        }
        Pal.StrokeRound(g, r, 10 * dp, Enabled && _hover ? Pal.Primary : Pal.Outline, _hover ? 1.8f * dp : Math.Max(1f, dp));

        // Hex label in black or white, whichever reads better on the colour.
        double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        bool dark = c.A < 110 || lum > 0.6;
        Color fore = c.A < 110 ? Pal.Text : dark ? Color.FromArgb(255, 20, 16, 50) : Color.White;
        string label = _hex.Length == 9 ? "#" + _hex[3..] + (c.A < 255 ? $"  {c.A * 100 / 255}%" : "") : _hex;
        Pal.DrawText(g, label, Font, Enabled ? fore : Pal.TextFaint, r, StringAlignment.Center);
    }
}

/// <summary>Edits a <see cref="FillSpec"/>: solid, linear gradient or circular gradient with up to three colours.</summary>
public sealed class FillEditor : UserControl
{
    readonly MCombo _type = new() { Width = 180 };
    readonly MCombo _count = new() { Width = 120 };
    readonly ColorButton _c1 = new(), _c2 = new(), _c3 = new();
    readonly Label _l1 = UiText.Label("Color"), _l2 = UiText.Label("Color 2"), _l3 = UiText.Label("Color 3");
    readonly MSlider _angle = new() { Minimum = 0, Maximum = 360, Width = 250, Height = 30, Suffix = "°" };
    readonly Label _angleLabel = UiText.Label("Angle");
    readonly FlowLayoutPanel _countRow, _row1, _row2, _row3, _angleRow;
    FillSpec _fill = new();
    bool _updating;

    public event EventHandler? Changed;

    public FillEditor()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        _type.Items.AddRange(new object[] { "Solid color", "Linear gradient", "Circular gradient" });
        _count.Items.AddRange(new object[] { "2 colors", "3 colors" });

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _countRow = Row(_type, _count);
        _row1 = Row(_l1, _c1);
        _row2 = Row(_l2, _c2);
        _row3 = Row(_l3, _c3);
        _angleRow = Row(_angleLabel, _angle);
        foreach (var r in new[] { _countRow, _row1, _row2, _row3, _angleRow }) root.Controls.Add(r);
        Controls.Add(root);

        _type.SelectedIndexChanged += (_, _) => Commit();
        _count.SelectedIndexChanged += (_, _) => Commit();
        _angle.ValueChanged += (_, _) => Commit();
        foreach (var c in new[] { _c1, _c2, _c3 }) c.ColorChanged += (_, _) => Commit();
    }

    static FlowLayoutPanel Row(params Control[] items)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent };
        row.Controls.AddRange(items);
        return row;
    }

    /// <summary>Edits this fill in place and raises <see cref="Changed"/> on every modification.</summary>
    public void Bind(FillSpec fill)
    {
        _fill = fill;
        _updating = true;
        _type.SelectedIndex = (int)fill.Type;
        _count.SelectedIndex = fill.ColorCount >= 3 ? 1 : 0;
        _c1.Hex = fill.Color1;
        _c2.Hex = fill.Color2;
        _c3.Hex = fill.Color3;
        _angle.Value = Math.Clamp((int)Math.Round(fill.Angle), 0, 360);
        _updating = false;
        UpdateVisibility();
    }

    void Commit()
    {
        if (_updating) return;
        _fill.Type = (FillType)Math.Max(_type.SelectedIndex, 0);
        _fill.ColorCount = _count.SelectedIndex == 1 ? 3 : 2;
        _fill.Color1 = _c1.Hex;
        _fill.Color2 = _c2.Hex;
        _fill.Color3 = _c3.Hex;
        _fill.Angle = _angle.Value;
        UpdateVisibility();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    void UpdateVisibility()
    {
        bool gradient = _fill.Type != FillType.Solid;
        _count.Visible = gradient;
        _l1.Text = !gradient ? "Color" : _fill.Type == FillType.Radial ? "Center" : "Color 1";
        _l2.Text = _fill.Type == FillType.Radial && _fill.ColorCount < 3 ? "Edge" : "Color 2";
        _l3.Text = _fill.Type == FillType.Radial ? "Edge" : "Color 3";
        _row2.Visible = gradient;
        _row3.Visible = gradient && _fill.ColorCount >= 3;
        _angleRow.Visible = _fill.Type == FillType.Linear;
    }
}
