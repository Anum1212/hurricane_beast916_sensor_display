using HurricaneBeastPanel.Ui;

namespace HurricaneBeastPanel;

/// <summary>Edits either the theme background or the selected element.</summary>
public sealed class PropertyPanel : UserControl
{
    const string MediaFilter =
        "Images and videos|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.m4v;*.mov;*.avi;*.mkv;*.webm;*.wmv|All files|*.*";

    static readonly ElementType[] FrameShapes =
        { ElementType.Rectangle, ElementType.Ellipse, ElementType.Triangle, ElementType.Diamond, ElementType.Pentagon, ElementType.Hexagon };

    readonly FlowLayoutPanel _root = new()
    {
        Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(2, 2, 6, 2),
        BackColor = Pal.Bg,
    };

    // Background
    readonly Card _gBackground = Group("Background");
    readonly FillEditor _bgFill = new();
    readonly Label _canvasInfo = new() { AutoSize = true };
    readonly MTextBox _bgMedia = new() { Width = 130, ReadOnly = true };
    readonly MButton _bgBrowse = new() { Text = "Browse...", Width = 80 };
    readonly MButton _bgClear = new() { Text = "Clear", Width = 60 };
    readonly MCombo _bgFit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };

    // General
    readonly Card _gGeneral = Group("Element");
    readonly MTextBox _name = new() { Width = 220 };
    readonly MCheck _visible = new() { Text = "Visible", AutoSize = true };
    readonly MNumeric _x = Num(-5000, 5000), _y = Num(-5000, 5000), _w = Num(1, 5000), _h = Num(1, 5000);
    readonly MNumeric _rotation = Num(0, 360);

    // Text
    readonly Card _gText = Group("Text");
    readonly MTextBox _text = new() { Multiline = true, Width = 330, Height = 54, AcceptsReturn = true };
    readonly MCombo _tokenBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    readonly MButton _insertToken = new() { Text = "Insert", Width = 70 };
    readonly MCombo _dateBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    readonly MButton _insertDate = new() { Text = "Insert", Width = 70 };
    readonly List<string> _dateTokens = new();
    readonly MCombo _font = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly MNumeric _fontSize = Num(4, 600);
    readonly MCheck _bold = new() { Text = "Bold", AutoSize = true };
    readonly MCheck _italic = new() { Text = "Italic", AutoSize = true };
    readonly MCombo _align = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    readonly FillEditor _textFill = new();

    // Media
    readonly Card _gMedia = Group("Image / GIF / video");
    readonly MTextBox _mediaPath = new() { Width = 130, ReadOnly = true };
    readonly MButton _mediaBrowse = new() { Text = "Browse...", Width = 80 };
    readonly MButton _mediaClear = new() { Text = "Clear", Width = 60 };
    readonly MCombo _mediaFit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    readonly MCombo _frameShape = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };

    // Shape / chart appearance
    readonly Card _gShape = Group("Shape");
    readonly MCheck _fillOn = new() { Text = "Filled", AutoSize = true };
    readonly FillEditor _shapeFill = new();
    readonly MCheck _remainOn = new() { Text = "Color the remaining part", AutoSize = true };
    readonly FillEditor _remainFill = new();
    readonly MCheck _borderOn = new() { Text = "Border", AutoSize = true };
    readonly MNumeric _borderWidth = Num(0, 200, 1);
    readonly ColorButton _borderColor = new();
    readonly MNumeric _radius = Num(0, 500, 1);
    readonly MNumeric _ringThickness = Num(1, 1000, 0);
    readonly MNumeric _startAngle = Num(-360, 360), _sweepAngle = Num(1, 360);
    FlowLayoutPanel _fillRow = null!, _remainRow = null!, _borderRow = null!, _radiusRow = null!, _ringRow = null!, _angleRow = null!;

    // Widget (gauge / graph / meter)
    readonly Card _gWidget = Group("Widget");
    readonly MCombo _style = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    readonly MNumeric _segments = Num(2, 240);
    readonly MNumeric _samples = Num(4, 600);
    readonly MNumeric _lineWidth = Num(1, 40, 1);
    readonly ColorButton _accent = new();
    FlowLayoutPanel _segRow = null!, _samplesRow = null!, _accentRow = null!, _bind2Row = null!, _bind3Row = null!;

    // Glow
    readonly Card _gGlow = Group("Glow");
    readonly MSlider _glowSize = new() { Minimum = 0, Maximum = 40, Width = 260, Height = 30 };
    readonly ColorButton _glowColor = new();

    // Live value
    readonly Card _gBind = Group("Live value");
    readonly MCombo _bind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly MCombo _bind2 = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly MCombo _bind3 = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly MNumeric _bindMin = Num(-100000, 100000, 1), _bindMax = Num(-100000, 100000, 1);

    Theme? _theme;
    Element? _element;
    bool _updating;

    public event Action? Changed;

    static Card Group(string title) => new()
    {
        Title = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 12),
        MinimumSize = new Size(352, 0), MaximumSize = new Size(352, 0),
    };

    static MNumeric Num(int min, int max, int decimals = 0) => new()
    {
        Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = 92, Increment = decimals > 0 ? 0.5m : 1m,
    };

    static FlowLayoutPanel Row(params Control[] items)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8), BackColor = Color.Transparent };
        row.Controls.AddRange(items);
        return row;
    }

    static Label Lbl(string text) => UiText.Label(text);

    static void Fill(Card box, params Control[] items)
    {
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Top, BackColor = Color.Transparent };
        flow.Controls.AddRange(items);
        box.Controls.Add(flow);
    }

    public PropertyPanel()
    {
        BackColor = Pal.Bg;
        _canvasInfo.ForeColor = Pal.TextDim;
        _canvasInfo.BackColor = Color.Transparent;
        Pal.DarkScroll(_root);
        _bgFit.Items.AddRange(new object[] { "Fill (crop)", "Fit (letterbox)", "Stretch" });
        _mediaFit.Items.AddRange(new object[] { "Fill (crop)", "Fit (letterbox)", "Stretch" });
        Fill(_gBackground, _canvasInfo, _bgFill,
            Lbl("Image / GIF / video behind everything"),
            Row(_bgMedia, _bgBrowse, _bgClear),
            Row(Lbl("Fit"), _bgFit));

        Fill(_gGeneral,
            Row(Lbl("Name"), _name),
            _visible,
            Row(Lbl("X"), _x, Lbl("Y"), _y),
            Row(Lbl("Width"), _w, Lbl("Height"), _h),
            Row(Lbl("Rotation"), _rotation, Lbl("degrees (drag the yellow handle)")));

        _align.Items.AddRange(new object[] { "Left", "Center", "Right" });
        _tokenBox.Items.AddRange(Tokens.All.Select(t => (object)$"{{{t.Key}}}  -  {t.Description}").ToArray());
        if (_tokenBox.Items.Count > 0) _tokenBox.SelectedIndex = 0;
        _font.Items.AddRange(FontStore.AllFamilies().Select(f => (object)f).ToArray());
        foreach (var (caption, token) in DateTimeFormats.Flat())
        {
            _dateBox.Items.Add(caption);
            _dateTokens.Add(token);
        }
        if (_dateBox.Items.Count > 0) _dateBox.SelectedIndex = 0;
        Fill(_gText,
            _text,
            Row(_tokenBox, _insertToken),
            Row(_dateBox, _insertDate),
            Row(Lbl("Font"), _font, Lbl("Size"), _fontSize),
            Row(_bold, _italic, Lbl("Align"), _align),
            Lbl("Text color"),
            _textFill);

        _frameShape.Items.AddRange(FrameShapes.Select(s => (object)s.ToString()).ToArray());
        Fill(_gMedia,
            Row(_mediaPath, _mediaBrowse, _mediaClear),
            Row(Lbl("Fit"), _mediaFit),
            Row(Lbl("Frame shape"), _frameShape));

        _fillRow = Row(_fillOn);
        _remainRow = Row(_remainOn);
        _borderRow = Row(_borderOn, Lbl("Thickness"), _borderWidth, _borderColor);
        _radiusRow = Row(Lbl("Corner radius"), _radius);
        _ringRow = Row(Lbl("Ring thickness"), _ringThickness);
        _angleRow = Row(Lbl("Start angle"), _startAngle, Lbl("Sweep"), _sweepAngle);
        Fill(_gShape, _fillRow, _shapeFill, _remainRow, _remainFill, _ringRow, _angleRow, _borderRow, _radiusRow);

        _style.Items.AddRange(WidgetCatalog.All.Select(i => (object)$"{i.Category}: {i.Name}").ToArray());
        _segRow = Row(Lbl("Segments / ticks / rows"), _segments);
        _samplesRow = Row(Lbl("History samples"), _samples, Lbl("Line width"), _lineWidth);
        _accentRow = Row(Lbl("Accent color"), _accent);
        Fill(_gWidget, Row(Lbl("Style"), _style), _segRow, _samplesRow, _accentRow);

        foreach (var cb in new[] { _bind, _bind2, _bind3 })
        {
            cb.Items.Add("(none)");
            cb.Items.AddRange(Tokens.NumericKeys.Cast<object>().ToArray());
        }
        _bind2Row = Row(Lbl("2nd value"), _bind2);
        _bind3Row = Row(Lbl("3rd value"), _bind3);
        Fill(_gBind, Row(_bind), _bind2Row, _bind3Row, Row(Lbl("0% at"), _bindMin, Lbl("100% at"), _bindMax));

        Fill(_gGlow, Row(Lbl("Size"), _glowSize), Row(Lbl("Color"), _glowColor));

        foreach (var g in new[] { _gBackground, _gGeneral, _gText, _gMedia, _gWidget, _gShape, _gGlow, _gBind }) _root.Controls.Add(g);
        Controls.Add(_root);
        Wire();
        ShowNothing();
    }

    void Wire()
    {
        void Hook(MNumeric n, Action apply) => n.ValueChanged += (_, _) => Edit(apply);
        void HookCheck(MCheck c, Action apply) => c.CheckedChanged += (_, _) => Edit(apply);

        _bgFill.Changed += (_, _) => Edit(() => { });
        _textFill.Changed += (_, _) => Edit(() => { });
        _shapeFill.Changed += (_, _) => Edit(() => { });
        _remainFill.Changed += (_, _) => Edit(() => { });

        // Background media
        _bgBrowse.Click += (_, _) =>
        {
            string? path = PickMedia();
            if (path == null || _theme == null) return;
            _bgMedia.Text = path;
            Edit(() => _theme.BackgroundMedia = path);
        };
        _bgClear.Click += (_, _) =>
        {
            _bgMedia.Text = "";
            Edit(() => _theme!.BackgroundMedia = "");
        };
        _bgFit.SelectedIndexChanged += (_, _) => Edit(() => _theme!.BackgroundMediaFit = (MediaFit)Math.Max(_bgFit.SelectedIndex, 0));

        // General
        _name.TextChanged += (_, _) => Edit(() => _element!.Name = _name.Text);
        HookCheck(_visible, () => _element!.Visible = _visible.Checked);
        Hook(_x, () => _element!.X = (float)_x.Value);
        Hook(_y, () => _element!.Y = (float)_y.Value);
        Hook(_w, () => _element!.Width = (float)_w.Value);
        Hook(_h, () => _element!.Height = (float)_h.Value);
        Hook(_rotation, () => _element!.Rotation = (float)_rotation.Value);

        // Text
        _text.TextChanged += (_, _) => Edit(() => _element!.Text = _text.Text);
        void InsertAtCursor(string token)
        {
            if (_element == null) return;
            int pos = Math.Min(_text.SelectionStart, _text.TextLength);
            _text.Text = _text.Text.Insert(pos, token);
            _text.SelectionStart = pos + token.Length;
            _text.Focus();
        }
        _insertToken.Click += (_, _) =>
        {
            if (_tokenBox.SelectedIndex >= 0) InsertAtCursor("{" + Tokens.All[_tokenBox.SelectedIndex].Key + "}");
        };
        _insertDate.Click += (_, _) =>
        {
            if (_dateBox.SelectedIndex >= 0) InsertAtCursor(_dateTokens[_dateBox.SelectedIndex]);
        };
        _font.SelectedIndexChanged += (_, _) => Edit(() => _element!.FontFamily = _font.SelectedItem?.ToString() ?? "Segoe UI");
        Hook(_fontSize, () => _element!.FontSize = (float)_fontSize.Value);
        HookCheck(_bold, () => _element!.Bold = _bold.Checked);
        HookCheck(_italic, () => _element!.Italic = _italic.Checked);
        _align.SelectedIndexChanged += (_, _) => Edit(() => _element!.Align = (HAlign)Math.Max(_align.SelectedIndex, 0));

        // Media
        _mediaBrowse.Click += (_, _) =>
        {
            string? path = PickMedia();
            if (path == null || _element == null) return;
            _mediaPath.Text = path;
            Edit(() => _element!.MediaPath = path);
        };
        _mediaClear.Click += (_, _) =>
        {
            _mediaPath.Text = "";
            Edit(() => _element!.MediaPath = "");
        };
        _mediaFit.SelectedIndexChanged += (_, _) => Edit(() => _element!.Fit = (MediaFit)Math.Max(_mediaFit.SelectedIndex, 0));
        _frameShape.SelectedIndexChanged += (_, _) =>
        {
            Edit(() => _element!.FrameShape = FrameShapes[Math.Max(_frameShape.SelectedIndex, 0)]);
            if (_element != null && !_updating) _radiusRow.Visible = _element.HasCorners;
        };

        // Shape and chart appearance
        HookCheck(_fillOn, () => { _element!.FillEnabled = _fillOn.Checked; _shapeFill.Enabled = _fillOn.Checked; });
        HookCheck(_remainOn, () => { _element!.RemainEnabled = _remainOn.Checked; _remainFill.Enabled = _remainOn.Checked; });
        HookCheck(_borderOn, () =>
        {
            _element!.BorderEnabled = _borderOn.Checked;
            _borderWidth.Enabled = _borderColor.Enabled = _borderOn.Checked;
        });
        Hook(_borderWidth, () => _element!.BorderWidth = (float)_borderWidth.Value);
        _borderColor.ColorChanged += (_, _) => Edit(() => _element!.BorderColor = _borderColor.Hex);
        Hook(_radius, () => _element!.CornerRadius = (float)_radius.Value);
        Hook(_ringThickness, () => _element!.RingThickness = (float)_ringThickness.Value);
        Hook(_startAngle, () => _element!.StartAngle = (float)_startAngle.Value);
        Hook(_sweepAngle, () => _element!.SweepAngle = (float)_sweepAngle.Value);

        // Glow
        _glowSize.ValueChanged += (_, _) => Edit(() => _element!.GlowSize = _glowSize.Value);
        _glowColor.ColorChanged += (_, _) => Edit(() => _element!.GlowColor = _glowColor.Hex);

        // Widget
        _style.SelectedIndexChanged += (_, _) =>
        {
            Edit(() =>
            {
                var info = WidgetCatalog.All[Math.Max(_style.SelectedIndex, 0)];
                var el = _element!;
                el.Style = info.Style;
                // A new style brings its own arc angles, thickness and counts.
                el.StartAngle = info.Start;
                el.SweepAngle = info.Sweep;
                if (info.Thickness > 0) el.RingThickness = info.Thickness;
                el.Segments = info.Segments;
                el.Samples = info.Samples;
                if (info.Bind2 && string.IsNullOrEmpty(el.Bind2)) el.Bind2 = "ram.load";
                if (info.Bind3 && string.IsNullOrEmpty(el.Bind3)) el.Bind3 = "gpu.load";
            });
            if (_element != null && !_updating) ShowElement(_element); // other styles use other settings
        };
        Hook(_segments, () => _element!.Segments = (int)_segments.Value);
        Hook(_samples, () => _element!.Samples = (int)_samples.Value);
        Hook(_lineWidth, () => _element!.LineWidth = (float)_lineWidth.Value);
        _accent.ColorChanged += (_, _) => Edit(() => _element!.AccentColor = _accent.Hex);
        _bind2.SelectedIndexChanged += (_, _) => Edit(() => _element!.Bind2 = _bind2.SelectedIndex <= 0 ? "" : _bind2.SelectedItem!.ToString()!);
        _bind3.SelectedIndexChanged += (_, _) => Edit(() => _element!.Bind3 = _bind3.SelectedIndex <= 0 ? "" : _bind3.SelectedItem!.ToString()!);

        // Live value
        _bind.SelectedIndexChanged += (_, _) => Edit(() => _element!.Bind = _bind.SelectedIndex <= 0 ? "" : _bind.SelectedItem!.ToString()!);
        Hook(_bindMin, () => _element!.BindMin = (float)_bindMin.Value);
        Hook(_bindMax, () => _element!.BindMax = (float)_bindMax.Value);
    }

    /// <summary>Asks for an image/video file and copies it into the app's media folder. Returns the stored path.</summary>
    string? PickMedia()
    {
        using var dlg = new OpenFileDialog { Filter = MediaFilter, Title = "Choose an image, GIF or video" };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return null;
        try { return MediaCache.Import(dlg.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), "Could not use that file: " + ex.Message, "Media", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
    }

    void Edit(Action apply)
    {
        if (_updating) return;
        if (_element == null && !_gBackground.Visible) return;
        apply();
        Changed?.Invoke();
    }

    void ShowNothing()
    {
        foreach (var g in new[] { _gBackground, _gGeneral, _gText, _gMedia, _gWidget, _gShape, _gGlow, _gBind }) g.Visible = false;
    }

    /// <summary>Edit the theme background.</summary>
    public void ShowBackground(Theme theme)
    {
        _theme = theme;
        _element = null;
        _updating = true;
        ShowNothing();
        _gBackground.Visible = true;
        _canvasInfo.Text = $"Canvas {theme.Width} x {theme.Height} ({(theme.IsVertical ? "vertical" : "horizontal")})";
        _bgFill.Bind(theme.Background);
        _bgMedia.Text = theme.BackgroundMedia;
        _bgFit.SelectedIndex = (int)theme.BackgroundMediaFit;
        _updating = false;
    }

    public void ShowElement(Element e)
    {
        _element = e;
        _updating = true;
        ShowNothing();
        _gGeneral.Visible = true;
        _name.Text = e.Name;
        _visible.Checked = e.Visible;
        RefreshGeometry();

        _gText.Visible = e.IsText;
        _gMedia.Visible = e.IsMedia;
        _gWidget.Visible = e.IsWidget;
        _gGlow.Visible = e.SupportsGlow;
        _glowSize.Value = (int)Math.Clamp(e.GlowSize, 0, 40);
        _glowColor.Hex = e.GlowColor;
        _gShape.Visible = !e.IsText;
        _gBind.Visible = !e.IsText && !e.IsMedia;

        if (e.IsText)
        {
            _text.Text = e.Text;
            if (!_font.Items.Contains(e.FontFamily)) _font.Items.Add(e.FontFamily);
            _font.SelectedItem = e.FontFamily;
            _fontSize.Value = (decimal)Math.Clamp(e.FontSize, 4, 600);
            _bold.Checked = e.Bold;
            _italic.Checked = e.Italic;
            _align.SelectedIndex = (int)e.Align;
            _textFill.Bind(e.Fill);
        }
        else
        {
            bool chart = e.IsChart, widget = e.IsWidget;
            var info = widget ? WidgetCatalog.Info(e.Style) : null;
            _gShape.Text = widget ? "Colors and shape" : chart ? "Chart" : e.IsMedia ? "Frame" : "Shape";
            _gBind.Text = widget || chart ? "Live value" : "Live value (makes a bar)";
            _fillOn.Text = chart ? "Filled part" : e.IsMedia ? "Backdrop behind the media" : "Filled";
            _remainOn.Text = widget ? "Track color (the unfilled part)" : "Color the remaining part";

            // Widgets always use their main colour, so the "Filled" tick box only applies to the other shapes.
            _fillRow.Visible = !e.IsLine && !widget;
            _shapeFill.Visible = !e.IsLine;
            _remainRow.Visible = _remainFill.Visible = chart || widget;
            _ringRow.Visible = widget ? info!.UsesRing : e.Type == ElementType.ChartCircular;
            _angleRow.Visible = widget ? info!.UsesAngles : e.Type is ElementType.ChartCircular or ElementType.ChartPie;
            _radiusRow.Visible = widget ? info!.UsesRadius : e.HasCorners;

            if (widget)
            {
                _style.SelectedIndex = Math.Max(WidgetCatalog.All.ToList().FindIndex(i => i.Style == e.Style), 0);
                _segments.Value = Math.Clamp(e.Segments, 2, 240);
                _samples.Value = Math.Clamp(e.Samples, 4, 600);
                _lineWidth.Value = (decimal)Math.Clamp(e.LineWidth, 1, 40);
                _accent.Hex = e.AccentColor;
                _segRow.Visible = info!.UsesSegments;
                _samplesRow.Visible = info.UsesSamples || info.UsesLineWidth;
                _samples.Visible = info.UsesSamples;
                _lineWidth.Visible = info.UsesLineWidth;
                _accentRow.Visible = info.UsesAccent;
                _bind2.SelectedIndex = string.IsNullOrEmpty(e.Bind2) ? 0 : Math.Max(_bind2.Items.IndexOf(e.Bind2), 0);
                _bind3.SelectedIndex = string.IsNullOrEmpty(e.Bind3) ? 0 : Math.Max(_bind3.Items.IndexOf(e.Bind3), 0);
            }
            _bind2Row.Visible = widget && info!.Bind2;
            _bind3Row.Visible = widget && info!.Bind3;

            _fillOn.Checked = e.FillEnabled;
            _shapeFill.Bind(e.Fill);
            _shapeFill.Enabled = e.FillEnabled;
            _remainOn.Checked = e.RemainEnabled;
            _remainFill.Bind(e.RemainFill);
            _remainFill.Enabled = e.RemainEnabled;

            _borderOn.Checked = e.BorderEnabled || e.IsLine;
            _borderOn.Enabled = !e.IsLine;
            _borderWidth.Value = (decimal)Math.Clamp(e.BorderWidth, 0, 200);
            _borderColor.Hex = e.BorderColor;
            _borderWidth.Enabled = _borderColor.Enabled = _borderOn.Checked;
            _radius.Value = (decimal)Math.Clamp(e.CornerRadius, 0, 500);
            _ringThickness.Value = (decimal)Math.Clamp(e.RingThickness, 1, 1000);
            _startAngle.Value = (decimal)Math.Clamp(e.StartAngle, -360, 360);
            _sweepAngle.Value = (decimal)Math.Clamp(e.SweepAngle, 1, 360);

            _mediaPath.Text = e.MediaPath;
            _mediaFit.SelectedIndex = (int)e.Fit;
            _frameShape.SelectedIndex = Math.Max(Array.IndexOf(FrameShapes, e.FrameShape), 0);

            _bind.SelectedIndex = string.IsNullOrEmpty(e.Bind) ? 0 : Math.Max(_bind.Items.IndexOf(e.Bind), 0);
            _bindMin.Value = (decimal)Math.Clamp(e.BindMin, -100000, 100000);
            _bindMax.Value = (decimal)Math.Clamp(e.BindMax, -100000, 100000);
        }
        _updating = false;
    }

    /// <summary>Updates the position, size and rotation boxes after the canvas changed the element.</summary>
    public void RefreshGeometry()
    {
        if (_element == null) return;
        bool was = _updating;
        _updating = true;
        _x.Value = (decimal)Math.Clamp(_element.X, -5000, 5000);
        _y.Value = (decimal)Math.Clamp(_element.Y, -5000, 5000);
        _w.Value = (decimal)Math.Clamp(_element.Width, 1, 5000);
        _h.Value = (decimal)Math.Clamp(_element.Height, 1, 5000);
        _rotation.Value = (decimal)Math.Clamp(_element.Rotation, 0, 360);
        _updating = was;
    }
}
