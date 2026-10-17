using System.Drawing.Drawing2D;

namespace HurricaneBeastPanel;

/// <summary>
/// Live theme preview with click-to-select, drag-to-move, handle resizing and a rotation handle.
/// Elements may overlap: a click picks the top-most one, Alt+click picks the one underneath.
/// </summary>
public sealed class CanvasView : Control
{
    enum Grip { None, NW, N, NE, E, SE, S, SW, W, Move, Rotate }

    const int HandleSize = 9;
    const float RotateOffsetPx = 34; // distance of the rotation handle above the top edge, in screen pixels

    Theme? _theme;
    Bitmap? _render;
    Grip _drag = Grip.None;
    PointF _dragStart;
    RectangleF _dragOrigin;
    PointF _origCenter;
    float _origRotation;
    float _zoom = 0.5f;

    public AppSettings Settings { get; set; } = new();
    public SensorSnapshot Snapshot { get; set; } = new();
    public Element? Selected { get; private set; }
    public bool Dragging => _drag != Grip.None;

    public event Action<Element?>? SelectionChanged;
    /// <summary>Raised while geometry changes (drag, nudge, rotate).</summary>
    public event Action? ThemeEdited;
    public event Action? DeleteRequested;

    public CanvasView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.FromArgb(40, 40, 46);
    }

    public Theme? Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Selected = null;
            UpdateSize();
            Rerender();
        }
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.05f, 2f);
            UpdateSize();
            Invalidate();
        }
    }

    /// <summary>Real pixels per theme pixel: the zoom level times the monitor's display scale.</summary>
    float Pix => _zoom * DeviceDpi / 96f;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateSize();
    }

    // The canvas size is the theme size times the zoom, in real pixels: DPI autoscaling must not resize it.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified) { }

    void UpdateSize()
    {
        if (_theme == null) return;
        Size = new Size((int)(_theme.Width * Pix), (int)(_theme.Height * Pix));
    }

    public void Select(Element? e)
    {
        if (Selected == e) return;
        Selected = e;
        SelectionChanged?.Invoke(e);
        Invalidate();
    }

    /// <summary>Re-draws the theme with the current sensor values.</summary>
    public void Rerender()
    {
        if (_theme == null) return;
        var old = _render;
        _render = ThemeRenderer.Render(_theme, Snapshot, Settings, _theme.Width, _theme.Height, preview: true);
        old?.Dispose();
        Invalidate();
    }

    // ---------- geometry helpers ----------

    static PointF Rotate(PointF v, float degrees)
    {
        double a = degrees * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        return new PointF((float)(v.X * cos - v.Y * sin), (float)(v.X * sin + v.Y * cos));
    }

    /// <summary>Screen position of a point given relative to the element's centre, in unrotated theme units.</summary>
    PointF LocalToScreen(Element e, float lx, float ly)
    {
        var c = e.Center;
        var r = Rotate(new PointF(lx, ly), e.Rotation);
        return new PointF((c.X + r.X) * Pix, (c.Y + r.Y) * Pix);
    }

    /// <summary>Theme point relative to the element's centre in its own (unrotated) frame.</summary>
    static PointF ToLocal(Element e, PointF themePoint)
    {
        var c = e.Center;
        return Rotate(new PointF(themePoint.X - c.X, themePoint.Y - c.Y), -e.Rotation);
    }

    IEnumerable<(Grip, PointF)> HandlePoints(Element e)
    {
        float hw = e.Width / 2, hh = e.Height / 2;
        yield return (Grip.NW, LocalToScreen(e, -hw, -hh));
        yield return (Grip.N, LocalToScreen(e, 0, -hh));
        yield return (Grip.NE, LocalToScreen(e, hw, -hh));
        yield return (Grip.E, LocalToScreen(e, hw, 0));
        yield return (Grip.SE, LocalToScreen(e, hw, hh));
        yield return (Grip.S, LocalToScreen(e, 0, hh));
        yield return (Grip.SW, LocalToScreen(e, -hw, hh));
        yield return (Grip.W, LocalToScreen(e, -hw, 0));
    }

    PointF RotateHandlePoint(Element e) => LocalToScreen(e, 0, -e.Height / 2 - RotateOffsetPx / Pix);

    // ---------- painting ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_render == null || _theme == null) return;

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(_render, 0, 0, _theme.Width * Pix, _theme.Height * Pix);
        if (Selected == null) return;

        var el = Selected;
        var state = g.Save();
        var c = el.Center;
        g.TranslateTransform(c.X * Pix, c.Y * Pix);
        g.RotateTransform(el.Rotation);
        float w = el.Width * Pix, h = el.Height * Pix;
        using (var dark = new Pen(Color.FromArgb(20, 20, 20), 1))
        using (var light = new Pen(Color.White, 1) { DashStyle = DashStyle.Dash })
        {
            g.DrawRectangle(dark, -w / 2, -h / 2, w, h);
            g.DrawRectangle(light, -w / 2, -h / 2, w, h);
        }
        g.Restore(state);

        var rot = RotateHandlePoint(el);
        var top = LocalToScreen(el, 0, -el.Height / 2);
        using (var line = new Pen(Color.White, 1)) g.DrawLine(line, top, rot);
        var rr = new RectangleF(rot.X - HandleSize / 2f - 1, rot.Y - HandleSize / 2f - 1, HandleSize + 2, HandleSize + 2);
        g.FillEllipse(Brushes.Yellow, rr);
        g.DrawEllipse(Pens.Black, rr);

        foreach (var (_, p) in HandlePoints(el))
        {
            var hr = new RectangleF(p.X - HandleSize / 2f, p.Y - HandleSize / 2f, HandleSize, HandleSize);
            g.FillRectangle(Brushes.White, hr);
            g.DrawRectangle(Pens.Black, hr.X, hr.Y, hr.Width, hr.Height);
        }
    }

    // ---------- hit testing ----------

    Grip HitGrip(Point p)
    {
        if (Selected == null) return Grip.None;
        var rot = RotateHandlePoint(Selected);
        if (Dist(p, rot) <= HandleSize) return Grip.Rotate;
        foreach (var (grip, pt) in HandlePoints(Selected))
            if (Math.Abs(p.X - pt.X) <= HandleSize && Math.Abs(p.Y - pt.Y) <= HandleSize) return grip;
        return Grip.None;
    }

    static float Dist(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    static bool Contains(Element el, PointF themePoint)
    {
        var l = ToLocal(el, themePoint);
        float hw = el.Width / 2, hh = el.Height / 2;
        if (el.IsLine) { hw += 6; hh += 10; } // make thin lines easy to grab
        return Math.Abs(l.X) <= hw && Math.Abs(l.Y) <= hh;
    }

    /// <summary>Elements under the point, top-most first.</summary>
    List<Element> HitElements(Point p)
    {
        var hits = new List<Element>();
        if (_theme == null) return hits;
        var pt = new PointF(p.X / Pix, p.Y / Pix);
        for (int i = _theme.Elements.Count - 1; i >= 0; i--)
        {
            var el = _theme.Elements[i];
            if (el.Visible && Contains(el, pt)) hits.Add(el);
        }
        return hits;
    }

    // ---------- mouse ----------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left || _theme == null) return;

        var grip = HitGrip(e.Location);
        if (grip == Grip.None)
        {
            var hits = HitElements(e.Location);
            Element? pick = hits.FirstOrDefault();
            if (ModifierKeys.HasFlag(Keys.Alt) && Selected != null && hits.Contains(Selected))
            {
                // Alt+click: step down to the element underneath the current one.
                int i = hits.IndexOf(Selected);
                pick = hits[(i + 1) % hits.Count];
            }
            Select(pick);
            if (pick == null) return;
            grip = Grip.Move;
        }

        _drag = grip;
        _dragStart = new PointF(e.X / Pix, e.Y / Pix);
        _dragOrigin = Selected!.Bounds;
        _origCenter = Selected.Center;
        _origRotation = Selected.Rotation;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag == Grip.None || Selected == null)
        {
            Cursor = CursorAt(e.Location);
            return;
        }

        var el = Selected;
        var mouse = new PointF(e.X / Pix, e.Y / Pix);

        if (_drag == Grip.Move)
        {
            el.X = MathF.Round(_dragOrigin.X + mouse.X - _dragStart.X);
            el.Y = MathF.Round(_dragOrigin.Y + mouse.Y - _dragStart.Y);
        }
        else if (_drag == Grip.Rotate)
        {
            float deg = MathF.Atan2(mouse.Y - _origCenter.Y, mouse.X - _origCenter.X) * 180f / MathF.PI + 90f;
            if (ModifierKeys.HasFlag(Keys.Shift)) deg = MathF.Round(deg / 15f) * 15f; // snap to 15 degrees
            deg = MathF.Round(deg) % 360f;
            el.Rotation = deg < 0 ? deg + 360f : deg;
        }
        else
        {
            ResizeFromHandle(el, mouse);
        }

        Rerender();
        ThemeEdited?.Invoke();
    }

    /// <summary>Resizes in the element's own rotated frame, keeping the opposite edge or corner fixed on screen.</summary>
    void ResizeFromHandle(Element el, PointF mouse)
    {
        const float min = 4;
        float w0 = _dragOrigin.Width, h0 = _dragOrigin.Height;
        var m = Rotate(new PointF(mouse.X - _origCenter.X, mouse.Y - _origCenter.Y), -_origRotation);

        float left = -w0 / 2, right = w0 / 2, top = -h0 / 2, bottom = h0 / 2;
        if (_drag is Grip.E or Grip.NE or Grip.SE) right = Math.Max(m.X, left + min);
        if (_drag is Grip.W or Grip.NW or Grip.SW) left = Math.Min(m.X, right - min);
        if (_drag is Grip.S or Grip.SE or Grip.SW) bottom = Math.Max(m.Y, top + min);
        if (_drag is Grip.N or Grip.NE or Grip.NW) top = Math.Min(m.Y, bottom - min);

        float nw = right - left, nh = bottom - top;
        var shift = Rotate(new PointF((left + right) / 2, (top + bottom) / 2), _origRotation);
        float cx = _origCenter.X + shift.X, cy = _origCenter.Y + shift.Y;
        el.Width = MathF.Round(nw);
        el.Height = MathF.Round(nh);
        el.X = MathF.Round(cx - el.Width / 2);
        el.Y = MathF.Round(cy - el.Height / 2);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _drag = Grip.None;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Ctrl+wheel grows or shrinks the selected element around its centre.
        if (Selected != null && ModifierKeys.HasFlag(Keys.Control))
        {
            float f = e.Delta > 0 ? 1.05f : 1f / 1.05f;
            var c = Selected.Center;
            Selected.Width = Math.Max(4, MathF.Round(Selected.Width * f));
            Selected.Height = Math.Max(4, MathF.Round(Selected.Height * f));
            Selected.X = MathF.Round(c.X - Selected.Width / 2);
            Selected.Y = MathF.Round(c.Y - Selected.Height / 2);
            Rerender();
            ThemeEdited?.Invoke();
            return;
        }
        base.OnMouseWheel(e);
    }

    Cursor CursorAt(Point p)
    {
        var grip = HitGrip(p);
        if (grip == Grip.Rotate) return Cursors.Hand;
        if (grip != Grip.None && Selected != null)
        {
            float baseAngle = grip switch
            {
                Grip.E => 0, Grip.SE => 45, Grip.S => 90, Grip.SW => 135,
                Grip.W => 180, Grip.NW => 225, Grip.N => 270, _ => 315,
            };
            float a = ((baseAngle + Selected.Rotation) % 180 + 180) % 180;
            if (a < 22.5f || a >= 157.5f) return Cursors.SizeWE;
            if (a < 67.5f) return Cursors.SizeNWSE;
            if (a < 112.5f) return Cursors.SizeNS;
            return Cursors.SizeNESW;
        }
        return HitElements(p).Count > 0 ? Cursors.SizeAll : Cursors.Default;
    }

    // ---------- keyboard ----------

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Selected == null) return;
        int step = e.Shift ? 10 : 1;
        switch (e.KeyCode)
        {
            case Keys.Left: Selected.X -= step; break;
            case Keys.Right: Selected.X += step; break;
            case Keys.Up: Selected.Y -= step; break;
            case Keys.Down: Selected.Y += step; break;
            case Keys.Delete: DeleteRequested?.Invoke(); return;
            default: return;
        }
        e.Handled = true;
        Rerender();
        ThemeEdited?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _render?.Dispose();
        base.Dispose(disposing);
    }
}
