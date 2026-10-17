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

/// <summary>Colour picker: hue ring with a saturation/brightness disc, opacity bar, hex and R/G/B boxes, eyedropper and presets. Reports changes live while open.</summary>
public sealed class ColorPickerDialog : UiForm
{
    static readonly string[] Presets =
    {
        "#FFFFFFFF", "#FFC8CDD8", "#FF8C97AC", "#FF2E3548", "#FF181C28", "#FF0C0E14", "#FF000000", "#00000000", "#FF8B7BFF", "#FF6C5CE7", "#FF352E7A", "#FF1F1B3F",
        "#FFFF5A5A", "#FFFF825A", "#FFFFD25A", "#FF46DC8C", "#FF46DCDC", "#FF5AAAFF", "#FF3A7BFF", "#FFAF87FF", "#FFFF6EA8", "#FFE63946", "#FF2A9D8F", "#FF264653",
        "#FFF4A261", "#FF8338EC", "#FF06D6A0", "#FFFFBE0B", "#FF00D2D2", "#FF2BF28A", "#FFFF9F1C", "#FFB5179E", "#FF4361EE", "#FF4CC9F0", "#FFF72585", "#FF7209B7",
    };

    readonly ColorWheel _wheel = new() { Location = new Point(70, 22), Size = new Size(300, 300) };
    readonly AlphaBar _alphaBar = new() { Location = new Point(20, 334), Size = new Size(400, 28) };
    readonly EyedropperButton _pipette = new() { Location = new Point(380, 22), Size = new Size(40, 40) };
    readonly MTextBox _hex = new() { Width = 118, Height = 38 };
    readonly MNumeric _r = Channel(), _g = Channel(), _b = Channel();
    readonly Action<Color>? _live;
    bool _updating;

    public Color SelectedColor { get; private set; }

    static MNumeric Channel() => new() { Minimum = 0, Maximum = 255, DecimalPlaces = 0, Width = 80, Height = 38 };

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
        ClientSize = new Size(440, 650);

        Controls.Add(_wheel);
        Controls.Add(_pipette);
        Controls.Add(_alphaBar);

        _hex.Location = new Point(20, 380);
        _r.Location = new Point(150, 380);
        _g.Location = new Point(238, 380);
        _b.Location = new Point(326, 380);
        Controls.AddRange(new Control[] { _hex, _r, _g, _b });

        var captions = new[] { ("#", 20, 118), ("R", 150, 80), ("G", 238, 80), ("B", 326, 80) };
        foreach (var (text, x, w) in captions)
        {
            var l = UiText.Label(text, dim: false);
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.SetBounds(x, 422, w, 24);
            Controls.Add(l);
        }

        var presetsLabel = UiText.Label("PRESETS", points: 8.5f);
        presetsLabel.Location = new Point(22, 458);
        Controls.Add(presetsLabel);
        var grid = new SwatchGrid(Presets) { Location = new Point(20, 482), Size = new Size(400, 100) };
        grid.Picked += c => SetColor(c);
        Controls.Add(grid);

        var ok = new MButton { Text = "OK", Style = ButtonStyle.Filled, Location = new Point(236, 598), Size = new Size(90, 38) };
        var cancel = new MButton { Text = "Cancel", Style = ButtonStyle.Text, Location = new Point(334, 598), Size = new Size(86, 38) };
        Controls.Add(ok);
        Controls.Add(cancel);
        ok.Click += (_, _) => DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        _wheel.ColorChanged += (_, _) => SetColor(Hsv.ToColor(_wheel.Hue, _wheel.Saturation, _wheel.Value, SelectedColor.A), fromWheel: true);
        _alphaBar.AlphaChanged += (_, _) => SetColor(Color.FromArgb(_alphaBar.Alpha, SelectedColor), fromWheel: true);
        foreach (var n in new[] { _r, _g, _b })
            n.ValueChanged += (_, _) => { if (!_updating) SetColor(Color.FromArgb(SelectedColor.A, (int)_r.Value, (int)_g.Value, (int)_b.Value)); };
        _hex.TextChanged += (_, _) =>
        {
            if (_updating) return;
            string h = _hex.Text.Trim().TrimStart('#');
            if (h.Length is 6 or 8 && h.All(Uri.IsHexDigit)) SetColor(ColorUtil.Parse(h), fromHex: true);
        };
        _pipette.Click += (_, _) =>
        {
            using var picker = new ScreenPickerForm();
            if (picker.ShowDialog(this) == DialogResult.OK) SetColor(Color.FromArgb(SelectedColor.A, picker.Result));
        };

        SetColor(initial);
        DpiHelper.Fit(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    static string Format(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : ColorUtil.ToHex(c);

    void SetColor(Color c, bool fromWheel = false, bool fromHex = false)
    {
        _updating = true;
        SelectedColor = c;
        if (!fromWheel)
        {
            var (h, s, v) = Hsv.From(c);
            if (s < 0.002f || v < 0.002f) h = _wheel.Hue; // grey and black have no hue: keep the ring where it was
            _wheel.SetHsv(h, s, v);
        }
        _alphaBar.BaseColor = c;
        _alphaBar.Alpha = c.A;
        _r.Value = c.R; _g.Value = c.G; _b.Value = c.B;
        if (!fromHex) _hex.Text = Format(c);
        _updating = false;
        _live?.Invoke(c);
    }

    /// <summary>Builds the dialog without showing it (for screenshots in development).</summary>
    internal static ColorPickerDialog CreateForTest(Color initial) => new(initial, null);

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

/// <summary>Edits a <see cref="FillSpec"/>: solid, linear gradient or circular gradient with any number of colours.</summary>
public sealed class FillEditor : UserControl
{
    readonly MCombo _type = new() { Width = 180 };
    readonly MButton _add = new() { Text = "Add color", Glyph = Glyph.Add, Style = ButtonStyle.Outlined };
    readonly FlowLayoutPanel _stopsPanel;
    readonly List<(Label label, ColorButton button, MButton remove, FlowLayoutPanel row)> _stopRows = new();
    readonly List<string> _stops = new();
    readonly MSlider _angle = new() { Minimum = 0, Maximum = 360, Width = 250, Height = 30, Suffix = "°" };
    readonly Label _angleLabel = UiText.Label("Angle");
    readonly FlowLayoutPanel _typeRow, _addRow, _angleRow;
    FillSpec _fill = new();
    bool _updating;

    public event EventHandler? Changed;

    public FillEditor()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        _type.Items.AddRange(new object[] { "Solid color", "Linear gradient", "Circular gradient" });

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _typeRow = Row(_type);
        _stopsPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty, BackColor = Color.Transparent };
        _addRow = Row(_add);
        _angleRow = Row(_angleLabel, _angle);
        foreach (var r in new Control[] { _typeRow, _stopsPanel, _addRow, _angleRow }) root.Controls.Add(r);
        Controls.Add(root);

        _type.SelectedIndexChanged += (_, _) => Commit();
        _angle.ValueChanged += (_, _) => Commit();
        _add.Click += (_, _) =>
        {
            if (_stops.Count >= FillSpec.MaxStops) return;
            _stops.Add(_stops[^1]);
            RebuildStops();
            Commit();
        };
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
        _stops.Clear();
        _stops.AddRange(fill.Stops is { Count: > 0 } ? fill.Stops : fill.Colors().Select(ColorUtil.ToHex));
        while (_stops.Count < 2) _stops.Add(_stops[^1]);
        _type.SelectedIndex = (int)fill.Type;
        _angle.Value = Math.Clamp((int)Math.Round(fill.Angle), 0, 360);
        RebuildStops();
        _updating = false;
        UpdateVisibility();
    }

    /// <summary>One row per colour: a solid fill shows only the first, a gradient shows them all with a remove button.</summary>
    void RebuildStops()
    {
        bool gradient = _fill.Type != FillType.Solid && _type.SelectedIndex != 0;
        int shown = gradient ? _stops.Count : 1;
        bool wasUpdating = _updating;
        _updating = true;
        _stopsPanel.SuspendLayout();
        while (_stopRows.Count < shown)
        {
            int index = _stopRows.Count;
            var label = UiText.Label("Color");
            var button = new ColorButton();
            var remove = new MButton { Text = "", Glyph = Glyph.Close, Style = ButtonStyle.Text, Width = 40 };
            button.ColorChanged += (_, _) =>
            {
                if (_updating || index >= _stops.Count) return;
                _stops[index] = button.Hex;
                Commit();
            };
            remove.Click += (_, _) =>
            {
                if (_stops.Count <= 2 || index >= _stops.Count) return;
                _stops.RemoveAt(index);
                RebuildStops();
                Commit();
            };
            var row = Row(label, button, remove);
            _stopRows.Add((label, button, remove, row));
            _stopsPanel.Controls.Add(row);
        }
        for (int i = 0; i < _stopRows.Count; i++)
        {
            var (label, button, remove, row) = _stopRows[i];
            row.Visible = i < shown;
            if (i >= shown) continue;
            button.Hex = _stops[i];
            label.Text = !gradient ? "Color"
                : _fill.Type == FillType.Radial ? (i == 0 ? "Center" : i == shown - 1 ? "Edge" : $"Color {i + 1}")
                : $"Color {i + 1}";
            remove.Visible = gradient && _stops.Count > 2;
        }
        _stopsPanel.ResumeLayout(true);
        _updating = wasUpdating;
    }

    void Commit()
    {
        if (_updating) return;
        _fill.Type = (FillType)Math.Max(_type.SelectedIndex, 0);
        _fill.Stops = new List<string>(_stops);
        // Keep the older fields in step for code that still reads them.
        _fill.Color1 = _stops[0];
        _fill.Color2 = _stops[1];
        _fill.Color3 = _stops.Count > 2 ? _stops[2] : _stops[1];
        _fill.ColorCount = Math.Min(_stops.Count, 3);
        _fill.Angle = _angle.Value;
        UpdateVisibility();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    void UpdateVisibility()
    {
        bool gradient = _fill.Type != FillType.Solid;
        RebuildStops();
        _addRow.Visible = gradient;
        _add.Enabled = _stops.Count < FillSpec.MaxStops;
        _angleRow.Visible = _fill.Type == FillType.Linear;
    }
}
