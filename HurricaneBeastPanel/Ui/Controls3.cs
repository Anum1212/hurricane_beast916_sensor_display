namespace HurricaneBeastPanel.Ui;

public sealed class ThumbItem
{
    public string Name = "";
    public string Section = "";
    /// <summary>Small label drawn on the card corner (for example "ACTIVE").</summary>
    public string Badge = "";
    public object? Tag;
    public Bitmap? Image;
}

/// <summary>
/// Scrollable grid of preview cards (image on black plus caption), painted directly so a few hundred stay fast.
/// Previews are created on demand, only for cards that become visible.
/// </summary>
public class ThumbGrid : Control
{
    List<ThumbItem> _items = new();
    int _scroll, _hover = -1;
    bool _dragBar;
    readonly List<(Rectangle rect, int index)> _cells = new();
    readonly List<(Rectangle rect, string text)> _headers = new();
    int _contentHeight;

    public event Action<ThumbItem>? ItemClicked;
    public event Action<ThumbItem>? ItemRightClicked;

    /// <summary>Creates the preview image for an item (width x height in pixels). Called lazily while painting.</summary>
    public Func<ThumbItem, int, int, Bitmap?>? ImageProvider { get; set; }

    /// <summary>Card height divided by card width.</summary>
    public float CellAspect { get; set; } = 0.86f;
    /// <summary>Minimum card width in design units; wider grids get more columns.</summary>
    public float CellMinWidth { get; set; } = 150;

    public ThumbGrid()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Pal.Surface;
        Font = Pal.Ui(8.5f, FontStyle.Bold);
        Cursor = Cursors.Default;
    }

    /// <summary>Re-draws after item badges changed, keeping previews and scroll position.</summary>
    public void Refresh2() { Invalidate(); }

    public void SetItems(IEnumerable<ThumbItem> items, bool keepScroll = false)
    {
        int keep = _scroll;
        foreach (var old in _items) old.Image?.Dispose();
        _items = items.ToList();
        _scroll = keepScroll ? keep : 0;
        _hover = -1;
        Relayout();
        Invalidate();
    }

    float Dp => Pal.Dp(this);

    int Columns => Math.Max(1, (int)((Width - 12 * Dp) / (CellMinWidth * Dp)));

    void Relayout()
    {
        _cells.Clear();
        _headers.Clear();
        if (Width < 20) return;
        float dp = Dp;
        int pad = (int)(10 * dp), gap = (int)(10 * dp), cols = Columns;
        int cw = (Width - pad * 2 - gap * (cols - 1) - (int)(8 * dp)) / cols;
        int ch = (int)(cw * CellAspect);
        int y = pad, col = 0;
        string section = "";
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Section != section)
            {
                section = _items[i].Section;
                if (col != 0) { y += ch + gap; col = 0; }
                if (!string.IsNullOrEmpty(section))
                {
                    _headers.Add((new Rectangle(pad, y, Width - pad * 2, (int)(30 * dp)), section));
                    y += (int)(34 * dp);
                }
            }
            _cells.Add((new Rectangle(pad + col * (cw + gap), y, cw, ch), i));
            if (++col == cols) { col = 0; y += ch + gap; }
        }
        if (col != 0) y += ch + gap;
        _contentHeight = y + pad;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    int MaxScroll => Math.Max(0, _contentHeight - Height);

    protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }

    int CellAt(Point p)
    {
        foreach (var (rect, index) in _cells)
        {
            var r = rect; r.Offset(0, -_scroll);
            if (r.Contains(p)) return index;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragBar)
        {
            _scroll = (int)(Math.Clamp(e.Y / (float)Height, 0f, 1f) * MaxScroll);
            Invalidate();
        }
        else
        {
            int i = CellAt(e.Location);
            if (i != _hover) { _hover = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (MaxScroll > 0 && e.X > Width - 14 * Dp) { _dragBar = true; OnMouseMove(e); return; }
        int i = CellAt(e.Location);
        if (i >= 0 && e.Button == MouseButtons.Left) ItemClicked?.Invoke(_items[i]);
        else if (i >= 0 && e.Button == MouseButtons.Right) ItemRightClicked?.Invoke(_items[i]);
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _dragBar = false; base.OnMouseUp(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _scroll = Math.Clamp(_scroll - Math.Sign(e.Delta) * (int)(110 * Dp), 0, MaxScroll);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Dp;
        g.Clear(BackColor);

        foreach (var (rect, text) in _headers)
        {
            var r = rect; r.Offset(0, -_scroll);
            if (r.Bottom < 0 || r.Top > Height) continue;
            Pal.DrawText(g, text.ToUpperInvariant(), Pal.Ui(8f, FontStyle.Bold), Pal.Primary, r);
        }

        foreach (var (rect, index) in _cells)
        {
            var r = rect; r.Offset(0, -_scroll);
            if (r.Bottom < 0 || r.Top > Height) continue;
            var item = _items[index];
            bool hover = index == _hover;

            Pal.FillRound(g, r, 12 * dp, hover ? Pal.SurfaceHi : Pal.SurfaceLo);
            Pal.StrokeRound(g, r, 12 * dp, hover ? Pal.Primary : Pal.Alpha(Pal.Outline, 140), hover ? 1.6f * dp : Math.Max(1f, dp));

            float capH = 30 * dp;
            var preview = new RectangleF(r.X + 7 * dp, r.Y + 7 * dp, r.Width - 14 * dp, r.Height - capH - 12 * dp);
            if (item.Image == null && ImageProvider != null)
                item.Image = ImageProvider(item, Math.Max(8, (int)preview.Width), Math.Max(8, (int)preview.Height));
            using (var path = Pal.Round(preview, 8 * dp))
            {
                using var black = new SolidBrush(Color.Black);
                g.FillPath(black, path);
                if (item.Image != null)
                {
                    var state = g.Save();
                    g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect);
                    g.DrawImage(item.Image, preview);
                    g.Restore(state);
                }
                using var pen = new Pen(Pal.Alpha(Pal.Outline, 190), Math.Max(1f, dp));
                g.DrawPath(pen, path);
            }
            if (item.Badge.Length > 0)
            {
                var tag = new RectangleF(preview.X + 8 * dp, preview.Y + 8 * dp, TextRenderer.MeasureText(item.Badge, Pal.Ui(7.5f, FontStyle.Bold)).Width + 14 * dp, 20 * dp);
                Pal.FillRound(g, tag, 10 * dp, Pal.Primary);
                Pal.DrawText(g, item.Badge, Pal.Ui(7.5f, FontStyle.Bold), Color.FromArgb(255, 28, 20, 78), tag, StringAlignment.Center);
            }
            Pal.DrawText(g, item.Name.ToUpperInvariant(), Pal.Ui(8f, FontStyle.Bold), hover ? Pal.Text : Pal.TextDim,
                new RectangleF(r.X + 6 * dp, r.Bottom - capH, r.Width - 12 * dp, capH - 2 * dp), StringAlignment.Center);
        }

        if (MaxScroll > 0)
        {
            float thumbH = Math.Max(30 * dp, Height * Height / (float)_contentHeight);
            float y = (Height - thumbH) * _scroll / MaxScroll;
            Pal.FillRound(g, new RectangleF(Width - 8 * dp, y, 4 * dp, thumbH), 2 * dp, Pal.Alpha(Pal.TextFaint, 200));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (var i in _items) i.Image?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class LayerRow
{
    public string Name = "";
    public IconKind Icon;
    public bool Visible = true;
    public bool CanHide = true;
}

/// <summary>Layer list: type icon, name and an eye toggle per row; selected row highlighted.</summary>
public class LayerList : Control
{
    List<LayerRow> _rows = new();
    int _selected = -1, _hover = -1, _scroll;
    bool _dragBar;

    public event Action<int>? SelectionChanged;
    public event Action<int>? VisibilityToggled;

    public LayerList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Pal.SurfaceLo;
        Font = Pal.Ui(9.5f);
        TabStop = true;
    }

    float Dp => Pal.Dp(this);
    int Row => (int)(38 * Dp);
    int MaxScroll => Math.Max(0, _rows.Count * Row + (int)(8 * Dp) - Height);

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            _selected = value;
            EnsureVisible(value);
            Invalidate();
        }
    }

    public void SetRows(List<LayerRow> rows, int selected)
    {
        _rows = rows;
        _selected = selected;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
        EnsureVisible(selected);
        Invalidate();
    }

    void EnsureVisible(int index)
    {
        if (index < 0) return;
        int top = index * Row + (int)(4 * Dp), bottom = top + Row;
        if (top < _scroll) _scroll = top - (int)(4 * Dp);
        else if (bottom > _scroll + Height) _scroll = bottom - Height + (int)(4 * Dp);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    int IndexAt(int y)
    {
        int rel = y + _scroll - (int)(4 * Dp);
        if (rel < 0) return -1;
        int i = rel / Row;
        return i < _rows.Count ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragBar)
        {
            _scroll = (int)(Math.Clamp(e.Y / (float)Height, 0f, 1f) * MaxScroll);
            Invalidate();
        }
        else
        {
            int i = IndexAt(e.Y);
            if (i != _hover) { _hover = i; Invalidate(); }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (MaxScroll > 0 && e.X > Width - 14 * Dp) { _dragBar = true; OnMouseMove(e); return; }
        int i = IndexAt(e.Y);
        if (i < 0) return;
        if (_rows[i].CanHide && e.X > Width - 44 * Dp - (MaxScroll > 0 ? 8 * Dp : 0))
        {
            VisibilityToggled?.Invoke(i);
            return;
        }
        if (i != _selected)
        {
            _selected = i;
            Invalidate();
            SelectionChanged?.Invoke(i);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _dragBar = false; base.OnMouseUp(e); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _scroll = Math.Clamp(_scroll - Math.Sign(e.Delta) * Row * 2, 0, MaxScroll);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int d = e.KeyCode == Keys.Down ? 1 : e.KeyCode == Keys.Up ? -1 : 0;
        if (d != 0 && _rows.Count > 0)
        {
            int i = Math.Clamp(_selected + d, 0, _rows.Count - 1);
            if (i != _selected) { _selected = i; EnsureVisible(i); Invalidate(); SelectionChanged?.Invoke(i); }
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Dp;
        g.Clear(BackColor);
        float scrollW = MaxScroll > 0 ? 8 * dp : 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            float y = 4 * dp + i * Row - _scroll;
            if (y + Row < 0 || y > Height) continue;
            var r = new RectangleF(6 * dp, y + 1, Width - 12 * dp - scrollW, Row - 2);
            var row = _rows[i];
            if (i == _selected) Pal.FillRound(g, r, 10 * dp, Pal.PrimaryContainer);
            else if (i == _hover) Pal.FillRound(g, r, 10 * dp, Pal.Alpha(Pal.SurfaceHi, 220));
            if (i == _selected) Pal.StrokeRound(g, r, 10 * dp, Pal.Alpha(Pal.Primary, 170), Math.Max(1f, dp));

            Color fore = !row.Visible ? Pal.TextFaint : i == _selected ? Pal.Text : Pal.TextDim;
            Icons.Draw(g, row.Icon, new RectangleF(r.X + 9 * dp, r.Y + 8 * dp, 20 * dp, r.Height - 16 * dp), i == _selected ? Pal.Primary : fore, 1.6f);
            Pal.DrawText(g, row.Name, Font, fore, new RectangleF(r.X + 38 * dp, r.Y, r.Width - 38 * dp - (row.CanHide ? 38 * dp : 8 * dp), r.Height));
            if (row.CanHide)
                Pal.DrawGlyph(g, row.Visible ? Glyph.Eye : Glyph.EyeOff, 10.5f, row.Visible ? Pal.TextDim : Pal.TextFaint,
                    new RectangleF(r.Right - 34 * dp, r.Y, 30 * dp, r.Height));
        }
        if (MaxScroll > 0)
        {
            float thumbH = Math.Max(24 * dp, Height * Height / (float)(_rows.Count * Row + 8 * dp));
            float ty = (Height - thumbH) * _scroll / MaxScroll;
            Pal.FillRound(g, new RectangleF(Width - 7 * dp, ty, 4 * dp, thumbH), 2 * dp, Pal.Alpha(Pal.TextFaint, 200));
        }
    }
}
