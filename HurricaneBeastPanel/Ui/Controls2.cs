using System.Globalization;

namespace HurricaneBeastPanel.Ui;

/// <summary>Rounded text input (single or multi-line) wrapping a plain TextBox.</summary>
public class MTextBox : Control
{
    readonly TextBox _tb = new() { BorderStyle = BorderStyle.None, BackColor = Pal.SurfaceHi, ForeColor = Pal.Text };
    bool _focus;

    public MTextBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl, true);
        BackColor = Pal.SurfaceHi;
        Font = Pal.Ui(9.5f);
        _tb.Font = Font;
        Controls.Add(_tb);
        Size = new Size(200, 36);
        _tb.Enter += (_, _) => { _focus = true; Invalidate(); };
        _tb.Leave += (_, _) => { _focus = false; Invalidate(); };
        _tb.TextChanged += (_, e) => OnTextChanged(e);
        _tb.KeyDown += (_, e) => OnKeyDown(e);
        LayoutInner();
    }

    public override string Text { get => _tb.Text; set => _tb.Text = value; }
    public bool Multiline { get => _tb.Multiline; set { _tb.Multiline = value; LayoutInner(); } }
    public bool ReadOnly { get => _tb.ReadOnly; set { _tb.ReadOnly = value; Invalidate(); } }
    public bool AcceptsReturn { get => _tb.AcceptsReturn; set => _tb.AcceptsReturn = value; }
    public int SelectionStart { get => _tb.SelectionStart; set => _tb.SelectionStart = value; }
    public int TextLength => _tb.TextLength;
    public string PlaceholderText { get => _tb.PlaceholderText; set => _tb.PlaceholderText = value; }
    public HorizontalAlignment TextAlign { get => _tb.TextAlign; set => _tb.TextAlign = value; }

    public void SelectAll() => _tb.SelectAll();
    public new bool Focus() => _tb.Focus();

    protected override void OnFontChanged(EventArgs e) { _tb.Font = Font; base.OnFontChanged(e); LayoutInner(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
    protected override void OnEnabledChanged(EventArgs e) { _tb.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _tb.Focus(); base.OnMouseDown(e); }

    void LayoutInner()
    {
        float dp = Pal.Dp(this);
        int pad = (int)(12 * dp);
        if (_tb.Multiline)
        {
            int vpad = (int)(8 * dp);
            _tb.SetBounds(pad, vpad, Math.Max(10, Width - pad * 2), Math.Max(10, Height - vpad * 2));
        }
        else
        {
            int h = _tb.PreferredHeight;
            _tb.SetBounds(pad, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - pad * 2), h);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, r, 10 * dp, Enabled ? Pal.SurfaceHi : Pal.SurfaceLo);
        Pal.StrokeRound(g, r, 10 * dp, _focus ? Pal.Primary : Pal.Alpha(Pal.Outline, 170), _focus ? 1.8f * dp : Math.Max(1f, dp));
    }
}

/// <summary>Numeric input with +/- steppers. Mirrors the parts of NumericUpDown the app uses.</summary>
public class MNumeric : Control
{
    readonly TextBox _tb = new() { BorderStyle = BorderStyle.None, BackColor = Pal.SurfaceHi, ForeColor = Pal.Text };
    decimal _value;
    bool _focus, _updating;

    public event EventHandler? ValueChanged;

    public decimal Minimum { get; set; }
    public decimal Maximum { get; set; } = 100;
    public int DecimalPlaces { get; set; }
    public decimal Increment { get; set; } = 1;

    public decimal Value
    {
        get => _value;
        set
        {
            decimal v = Math.Round(Math.Clamp(value, Minimum, Maximum), DecimalPlaces);
            ShowValue(v);
            if (v == _value) return;
            _value = v;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MNumeric()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl, true);
        BackColor = Pal.SurfaceHi;
        Font = Pal.Ui(9.5f);
        _tb.Font = Font;
        Controls.Add(_tb);
        Size = new Size(88, 34);
        _tb.Enter += (_, _) => { _focus = true; Invalidate(); };
        _tb.Leave += (_, _) => { _focus = false; Commit(); Invalidate(); };
        _tb.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Commit(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { Step(1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Down) { Step(-1); e.SuppressKeyPress = true; }
        };
        _tb.MouseWheel += (_, e) => { if (_tb.Focused) { Step(e.Delta > 0 ? 1 : -1); ((HandledMouseEventArgs)e).Handled = true; } };
        ShowValue(0);
        LayoutInner();
    }

    void ShowValue(decimal v)
    {
        _updating = true;
        _tb.Text = v.ToString("F" + DecimalPlaces, CultureInfo.CurrentCulture);
        _updating = false;
    }

    void Commit()
    {
        if (_updating) return;
        if (decimal.TryParse(_tb.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) ||
            decimal.TryParse(_tb.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
            Value = v;
        else
            ShowValue(_value);
    }

    void Step(int direction)
    {
        Commit();
        Value = _value + direction * Increment;
    }

    protected override void OnFontChanged(EventArgs e) { _tb.Font = Font; base.OnFontChanged(e); LayoutInner(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
    protected override void OnEnabledChanged(EventArgs e) { _tb.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        float dp = Pal.Dp(this);
        if (e.X > Width - 24 * dp && Enabled)
        {
            Step(e.Y < Height / 2 ? 1 : -1);
            return;
        }
        _tb.Focus();
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e) { if (Enabled) Step(e.Delta > 0 ? 1 : -1); }

    void LayoutInner()
    {
        float dp = Pal.Dp(this);
        int pad = (int)(10 * dp), h = _tb.PreferredHeight;
        _tb.SetBounds(pad, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - pad - (int)(26 * dp)), h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, r, 10 * dp, Enabled ? Pal.SurfaceHi : Pal.SurfaceLo);
        Pal.StrokeRound(g, r, 10 * dp, _focus ? Pal.Primary : Pal.Alpha(Pal.Outline, 170), _focus ? 1.8f * dp : Math.Max(1f, dp));

        float ax = Width - 22 * dp, cy = Height / 2f, w = 7 * dp, hgt = 3.2f * dp;
        using var pen = new Pen(Enabled ? Pal.TextDim : Pal.TextFaint, 1.6f * dp) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        float mid = ax + 7 * dp;
        g.DrawLines(pen, new[] { new PointF(mid - w / 2, cy - 3 * dp), new PointF(mid, cy - 3 * dp - hgt), new PointF(mid + w / 2, cy - 3 * dp) });
        g.DrawLines(pen, new[] { new PointF(mid - w / 2, cy + 3 * dp), new PointF(mid, cy + 3 * dp + hgt), new PointF(mid + w / 2, cy + 3 * dp) });
    }
}

/// <summary>A list of items; mirrors the part of ComboBox.ObjectCollection the app uses.</summary>
public sealed class ItemList : List<object> { }

/// <summary>Drop-down selector. Opens a rounded, scrollable popup that gets a search box when the list is long.</summary>
public class MCombo : Control
{
    int _selected = -1;
    bool _hover, _open;

    public event EventHandler? SelectedIndexChanged;

    public ItemList Items { get; } = new();
    /// <summary>Kept for API compatibility with ComboBox; the control is always a fixed list.</summary>
    public ComboBoxStyle DropDownStyle { get; set; }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = value < 0 || value >= Items.Count ? -1 : value;
            if (v == _selected) { Invalidate(); return; }
            _selected = v;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object? SelectedItem
    {
        get => _selected >= 0 && _selected < Items.Count ? Items[_selected] : null;
        set => SelectedIndex = value == null ? -1 : Items.IndexOf(value);
    }

    public MCombo()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Pal.SurfaceHi;
        Font = Pal.Ui(9.5f);
        Cursor = Cursors.Hand;
        TabStop = true;
        Size = new Size(200, 34);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (Enabled && e.Button == MouseButtons.Left) ShowPopup(); base.OnMouseDown(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down && _selected < Items.Count - 1) SelectedIndex = _selected + 1;
        else if (e.KeyCode == Keys.Up && _selected > 0) SelectedIndex = _selected - 1;
        else if (e.KeyCode is Keys.Enter or Keys.Space) ShowPopup();
        base.OnKeyDown(e);
    }

    void ShowPopup()
    {
        if (Items.Count == 0 || _open) return;
        float dp = Pal.Dp(this);
        var list = new PopupList(Items, _selected, Items.Count > 12)
        {
            Width = (int)Math.Max(Width, 230 * dp),
        };
        var host = new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size };
        var dd = new ToolStripDropDown
        {
            AutoClose = true, Padding = Padding.Empty, Margin = Padding.Empty, DropShadowEnabled = false,
            BackColor = Pal.SurfaceHi, Renderer = new PopupRenderer(),
        };
        dd.Items.Add(host);
        list.Chosen += i => { dd.Close(); SelectedIndex = i; };
        dd.Closed += (_, _) => { _open = false; Invalidate(); dd.Dispose(); };
        _open = true;
        Invalidate();
        dd.Show(this, new Point(0, Height + (int)(3 * dp)));
        list.FocusSearch();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Pal.FillRound(g, r, 10 * dp, Enabled ? Pal.SurfaceHi : Pal.SurfaceLo);
        Color border = _open || Focused ? Pal.Primary : _hover ? Pal.Alpha(Pal.Primary, 160) : Pal.Alpha(Pal.Outline, 170);
        Pal.StrokeRound(g, r, 10 * dp, border, _open || Focused ? 1.8f * dp : Math.Max(1f, dp));
        string text = SelectedItem?.ToString() ?? "";
        Pal.DrawText(g, text, Font, Enabled ? Pal.Text : Pal.TextFaint, new RectangleF(12 * dp, 0, Width - 40 * dp, Height));
        Pal.DrawGlyph(g, Glyph.ChevronDown, 8f, Enabled ? Pal.TextDim : Pal.TextFaint, new RectangleF(Width - 30 * dp, 0, 24 * dp, Height));
    }

    sealed class PopupRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Pal.SurfaceHi);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Pal.Outline);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }
}

/// <summary>Scrollable owner-drawn list used by <see cref="MCombo"/>, with optional type-to-filter search.</summary>
sealed class PopupList : Control
{
    const int RowDesign = 34;

    readonly ItemList _items;
    readonly int _current;
    readonly TextBox? _search;
    List<int> _view;
    int _hover = -1, _scroll;
    bool _dragBar;

    public event Action<int>? Chosen;

    float Dp => Pal.Dp(this);
    int Row => (int)(RowDesign * Dp);
    int SearchHeight => _search == null ? 0 : (int)(46 * Dp);

    public PopupList(ItemList items, int current, bool searchable)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        _items = items;
        _current = current;
        _view = Enumerable.Range(0, items.Count).ToList();
        BackColor = Pal.SurfaceHi;
        Font = Pal.Ui(9.5f);

        float dp = Pal.Dp(this);
        int visible = Math.Min(items.Count, 9);
        Height = (searchable ? (int)(46 * dp) : 0) + visible * (int)(RowDesign * dp) + (int)(8 * dp);
        Width = (int)(260 * dp);

        if (searchable)
        {
            _search = new TextBox { BorderStyle = BorderStyle.None, BackColor = Pal.SurfaceLo, ForeColor = Pal.Text, Font = Font, PlaceholderText = "Search..." };
            Controls.Add(_search);
            _search.TextChanged += (_, _) => ApplyFilter();
            _search.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter && _view.Count > 0) { Chosen?.Invoke(_view[Math.Max(_hover, 0) < _view.Count ? Math.Max(_hover, 0) : 0]); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { MoveHover(1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { MoveHover(-1); e.SuppressKeyPress = true; }
            };
        }
        // Start scrolled so the current item is visible.
        int idx = _view.IndexOf(current);
        if (idx > visible - 2) _scroll = (idx - 3) * Row;
        LayoutSearch();
    }

    public void FocusSearch() => _search?.Focus();

    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutSearch(); }

    void LayoutSearch()
    {
        if (_search == null) return;
        float dp = Dp;
        int h = _search.PreferredHeight;
        _search.SetBounds((int)(22 * dp), (int)(8 * dp) + Math.Max(0, ((int)(32 * dp) - h) / 2), Width - (int)(44 * dp), h);
    }

    void ApplyFilter()
    {
        string q = _search?.Text.Trim() ?? "";
        _view = Enumerable.Range(0, _items.Count)
            .Where(i => q.Length == 0 || (_items[i].ToString() ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _scroll = 0;
        _hover = _view.Count > 0 ? 0 : -1;
        Invalidate();
    }

    int ListHeight => Height - SearchHeight - (int)(8 * Dp);
    int MaxScroll => Math.Max(0, _view.Count * Row - ListHeight);

    void MoveHover(int d)
    {
        if (_view.Count == 0) return;
        _hover = Math.Clamp((_hover < 0 ? -1 : _hover) + d, 0, _view.Count - 1);
        int top = _hover * Row, bottom = top + Row;
        if (top < _scroll) _scroll = top;
        else if (bottom > _scroll + ListHeight) _scroll = bottom - ListHeight;
        Invalidate();
    }

    int IndexAt(int y)
    {
        int rel = y - SearchHeight - (int)(4 * Dp) + _scroll;
        if (y < SearchHeight || rel < 0) return -1;
        int i = rel / Row;
        return i < _view.Count ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragBar)
        {
            float f = Math.Clamp((e.Y - SearchHeight) / (float)Math.Max(1, ListHeight), 0f, 1f);
            _scroll = (int)(f * MaxScroll);
            Invalidate();
        }
        else
        {
            int i = IndexAt(e.Y);
            if (i != _hover) { _hover = i; Invalidate(); }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (MaxScroll > 0 && e.X > Width - 14 * Dp) { _dragBar = true; OnMouseMove(e); return; }
        int i = IndexAt(e.Y);
        if (i >= 0) Chosen?.Invoke(_view[i]);
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _dragBar = false; base.OnMouseUp(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _scroll = Math.Clamp(_scroll - Math.Sign(e.Delta) * Row * 2, 0, MaxScroll);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Dp;
        g.Clear(Pal.SurfaceHi);

        if (_search != null)
            Pal.FillRound(g, new RectangleF(10 * dp, 8 * dp, Width - 20 * dp, 32 * dp), 9 * dp, Pal.SurfaceLo);

        var clip = new Rectangle(0, SearchHeight, Width, Height - SearchHeight);
        g.SetClip(clip);
        int y0 = SearchHeight + (int)(4 * dp) - _scroll;
        for (int i = 0; i < _view.Count; i++)
        {
            int y = y0 + i * Row;
            if (y + Row < SearchHeight || y > Height) continue;
            var row = new RectangleF(6 * dp, y + 1, Width - 12 * dp - (MaxScroll > 0 ? 8 * dp : 0), Row - 2);
            bool isCurrent = _view[i] == _current;
            if (i == _hover) Pal.FillRound(g, row, 8 * dp, Pal.Alpha(Pal.Primary, 45));
            else if (isCurrent) Pal.FillRound(g, row, 8 * dp, Pal.PrimaryContainer);
            Pal.DrawText(g, _items[_view[i]].ToString() ?? "", Font, isCurrent ? Pal.Primary : Pal.Text,
                new RectangleF(row.X + 12 * dp, row.Y, row.Width - 34 * dp, row.Height));
            if (isCurrent) Pal.DrawGlyph(g, Glyph.Check, 9f, Pal.Primary, new RectangleF(row.Right - 28 * dp, row.Y, 22 * dp, row.Height));
        }
        if (_view.Count == 0)
            Pal.DrawText(g, "No matches", Font, Pal.TextFaint, new RectangleF(0, SearchHeight, Width, 40 * dp), StringAlignment.Center);

        if (MaxScroll > 0)
        {
            float trackH = ListHeight, thumbH = Math.Max(24 * dp, trackH * ListHeight / (_view.Count * Row));
            float thumbY = SearchHeight + (trackH - thumbH) * _scroll / MaxScroll;
            Pal.FillRound(g, new RectangleF(Width - 8 * dp, thumbY, 4 * dp, thumbH), 2 * dp, Pal.Alpha(Pal.TextFaint, 200));
        }
        g.ResetClip();
    }
}
