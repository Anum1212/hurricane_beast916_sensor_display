using HurricaneBeastPanel.Ui;

namespace HurricaneBeastPanel;

/// <summary>
/// Theme editor: category rail and thumbnail library on the left, canvas in the middle,
/// layers and settings on the right. Changes are pushed to the screen live.
/// </summary>
public sealed class ThemeEditorForm : UiForm
{
    readonly PanelEngine _engine;
    readonly AppSettings _settings;
    readonly Action<Theme> _livePush;
    readonly ToolTip _tips = UiKit.Tips();

    // top bar
    readonly Panel _top = new() { Dock = DockStyle.Top, Height = 66, BackColor = Pal.Bg };
    readonly Label _titleLabel = new() { AutoSize = false, ForeColor = Pal.Text, Font = Pal.Ui(14f, FontStyle.Bold), BackColor = Color.Transparent };
    readonly Label _subtitle = new() { AutoSize = false, ForeColor = Pal.TextDim, Font = Pal.Ui(9f), BackColor = Color.Transparent };
    readonly MButton _undoBtn = IconButton(Glyph.Undo), _redoBtn = IconButton(Glyph.Redo), _zoomOutBtn = IconButton(Glyph.ZoomOut), _zoomInBtn = IconButton(Glyph.ZoomIn);
    readonly MButton _rotateBtn = new() { Text = "Rotate layout", Glyph = Glyph.Rotate, Style = ButtonStyle.Outlined };
    readonly MButton _openBtn = new() { Text = "Open", Glyph = Glyph.Folder, Style = ButtonStyle.Outlined };
    readonly MButton _moreBtn = IconButton(Glyph.More);
    readonly MButton _saveBtn = new() { Text = "Save", Glyph = Glyph.Save, Style = ButtonStyle.Filled };
    readonly MCombo _zoom = new() { Width = 96 };
    readonly Chip _orientChip = new() { AutoSize = true, Dot = Pal.Primary };

    // left: rail + library
    readonly Panel _rail = new() { Dock = DockStyle.Left, Width = 86, BackColor = Pal.SurfaceLo, Padding = new Padding(2, 10, 2, 10) };
    readonly Panel _lib = new() { Dock = DockStyle.Left, Width = 372, BackColor = Pal.Surface };
    readonly List<RailButton> _railButtons = new();
    readonly Label _libTitle = new() { AutoSize = false, ForeColor = Pal.Primary, Font = Pal.Ui(13f, FontStyle.Bold), BackColor = Color.Transparent };
    readonly Label _libCount = new() { AutoSize = false, ForeColor = Pal.TextFaint, Font = Pal.Ui(11f), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent };
    readonly Label _libHint = new() { AutoSize = false, ForeColor = Pal.TextFaint, Font = Pal.Ui(8.5f), BackColor = Color.Transparent };
    readonly MTextBox _search = new() { PlaceholderText = "Search all widgets..." };
    readonly ThumbGrid _grid = new();
    readonly MButton _saveCustom = new() { Text = "Save selected layer", Glyph = Glyph.Save, Style = ButtonStyle.Tonal };
    LibraryCategory _category = Library.Categories[0];
    readonly Dictionary<string, List<LibraryItem>> _itemCache = new();

    // middle
    readonly Panel _canvasHost = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Pal.SurfaceLo };
    readonly CanvasView _canvas = new();
    readonly Label _hint = new()
    {
        Dock = DockStyle.Bottom, Height = 34, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Pal.TextFaint, Font = Pal.Ui(8.5f),
        BackColor = Pal.Bg,
        Text = "Drag: move   |   Handles: resize   |   Yellow dot: rotate   |   Alt+click: pick below   |   Ctrl+Z: undo",
    };

    // right: layers + inspector
    readonly Panel _right = new() { Dock = DockStyle.Right, Width = 404, BackColor = Pal.Bg, Padding = new Padding(10, 10, 10, 10) };
    readonly Card _layersCard = new() { Title = "Layers", Dock = DockStyle.Top, Height = 300, Padding = new Padding(10, 52, 10, 10) };
    readonly LayerList _layers = new() { Dock = DockStyle.Fill };
    readonly PropertyPanel _props = new() { Dock = DockStyle.Fill };
    readonly MButton _layerUp = IconButton(Glyph.Up), _layerDown = IconButton(Glyph.Down), _layerCopy = IconButton(Glyph.Copy), _layerDelete = IconButton(Glyph.Delete);

    // timers
    readonly System.Windows.Forms.Timer _pushTimer = new() { Interval = 200 };
    readonly System.Windows.Forms.Timer _snapTimer = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 100 };
    readonly System.Windows.Forms.Timer _commitTimer = new() { Interval = 450 };

    // state
    Theme _theme;
    string _savedJson;
    bool _dirty, _syncing, _closing, _restoring;
    List<Element?> _listMap = new();
    readonly Stack<string> _undo = new(), _redo = new();
    string _lastCommitted;

    /// <summary>Raised after a theme is written to disk, with its name.</summary>
    public event Action<string>? ThemeSaved;

    static MButton IconButton(string glyph) => new() { Glyph = glyph, Style = ButtonStyle.Text, Size = new Size(38, 36) };

    public ThemeEditorForm(Theme theme, PanelEngine engine, AppSettings settings, Action<Theme> livePush)
    {
        _engine = engine;
        _settings = settings;
        _livePush = livePush;
        _theme = theme.Clone();
        _savedJson = _theme.ToJson();
        _lastCommitted = _savedJson;

        Text = "Theme editor";
        Icon = SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        // Sizes are in 96-dpi design units; DpiHelper.Fit scales them to the monitor.
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1400, 800);
        float scale = DeviceDpi / 96f;
        Size = new Size(Math.Min(1640, (int)(work.Width * 0.95 / scale)), Math.Min(900, (int)(work.Height * 0.95 / scale)));
        MinimumSize = new Size(Math.Min(1180, Size.Width), Math.Min(640, Size.Height));

        BuildLayout();
        WireEvents();

        _canvas.Settings = settings;
        _canvas.Snapshot = engine.GetSnapshot();
        _canvas.Theme = _theme;

        SelectCategory(Library.Categories[0]);
        RefreshList(null);
        UpdateTitle();
        UpdateUndoButtons();

        _snapTimer.Start();
        _animTimer.Start();
        DpiHelper.Fit(this);
        Shown += (_, _) => { ApplyZoom(); LayoutTop(); LayoutLayersCard(); };
    }

    // ---------- layout ----------

    void BuildLayout()
    {
        // Top bar
        _top.Controls.AddRange(new Control[] { _titleLabel, _subtitle, _undoBtn, _redoBtn, _zoomOutBtn, _zoom, _zoomInBtn, _orientChip, _rotateBtn, _openBtn, _moreBtn, _saveBtn });
        _zoom.Items.AddRange(new object[] { "Fit", "25%", "33%", "50%", "66%", "75%", "100%", "150%", "200%" });
        _zoom.SelectedItem = "Fit";
        _top.Resize += (_, _) => LayoutTop();
        _top.Paint += (_, e) =>
        {
            using var pen = new Pen(Pal.Alpha(Pal.Outline, 150));
            e.Graphics.DrawLine(pen, 0, _top.Height - 1, _top.Width, _top.Height - 1);
        };

        // Rail
        int y = 10;
        foreach (var cat in Library.Categories)
        {
            var c = cat;
            var rb = new RailButton { Text = cat.Name, Icon = cat.Icon, Size = new Size(82, 68), Location = new Point(2, y) };
            rb.Click += (_, _) => SelectCategory(c);
            _tips.SetToolTip(rb, cat.Hint);
            _rail.Controls.Add(rb);
            _railButtons.Add(rb);
            y += 68;
        }

        // Library panel
        _lib.Controls.AddRange(new Control[] { _libTitle, _libCount, _search, _saveCustom, _libHint, _grid });
        _grid.ImageProvider = (item, w, h) => item.Tag is LibraryItem li ? Library.RenderThumb(li, w, h) : null;
        _lib.Resize += (_, _) => LayoutLibrary();

        // Centre
        _canvas.Location = new Point(24, 24);
        _canvasHost.Controls.Add(_canvas);
        Pal.DarkScroll(_canvasHost);
        _canvasHost.Resize += (_, _) => CenterCanvas();
        _canvasHost.Paint += PaintBezel; // a soft screen frame behind the canvas

        // Right
        _layersCard.Controls.Add(_layers);
        foreach (var b in new[] { _layerUp, _layerDown, _layerCopy, _layerDelete }) _layersCard.Controls.Add(b);
        _layersCard.Resize += (_, _) => LayoutLayersCard();
        _right.Controls.Add(_props);
        _right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Pal.Bg });
        _right.Controls.Add(_layersCard);

        // Dock order: the last control added is docked first.
        Controls.Add(_canvasHost);
        Controls.Add(_hint);
        Controls.Add(_right);
        Controls.Add(_lib);
        Controls.Add(_rail);
        Controls.Add(_top);

        _tips.SetToolTip(_undoBtn, "Undo (Ctrl+Z)");
        _tips.SetToolTip(_redoBtn, "Redo (Ctrl+Y)");
        _tips.SetToolTip(_zoomOutBtn, "Zoom out");
        _tips.SetToolTip(_zoomInBtn, "Zoom in");
        _tips.SetToolTip(_rotateBtn, "Switch between horizontal and vertical by turning the whole layout 90 degrees");
        _tips.SetToolTip(_moreBtn, "More: new theme, save as, delete");
        _tips.SetToolTip(_saveBtn, "Save theme (Ctrl+S)");
        _tips.SetToolTip(_layerUp, "Bring forward");
        _tips.SetToolTip(_layerDown, "Send back");
        _tips.SetToolTip(_layerCopy, "Duplicate (Ctrl+D)");
        _tips.SetToolTip(_layerDelete, "Delete (Del)");
    }

    void LayoutTop()
    {
        if (DpiHelper.Scaling) return;
        if (_top.Width < 100) return;
        float dp = Pal.Dp(this);
        int gap = (int)(8 * dp), h = (int)(38 * dp), y = (_top.Height - h) / 2, x = _top.Width - (int)(20 * dp);

        void Right(Control c, int width = 0)
        {
            if (c.AutoSize || width == 0) width = c.GetPreferredSize(Size.Empty).Width;
            c.SetBounds(x - width, y, width, h);
            x -= width + gap;
        }

        _saveBtn.Size = _saveBtn.GetPreferredSize(Size.Empty); Right(_saveBtn, _saveBtn.Width + (int)(10 * dp));
        _openBtn.Size = _openBtn.GetPreferredSize(Size.Empty); Right(_openBtn, _openBtn.Width + (int)(10 * dp));
        Right(_moreBtn, (int)(40 * dp));
        x -= (int)(14 * dp);
        _rotateBtn.Size = _rotateBtn.GetPreferredSize(Size.Empty); Right(_rotateBtn, _rotateBtn.Width + (int)(10 * dp));
        _orientChip.Size = _orientChip.GetPreferredSize(Size.Empty);
        _orientChip.SetBounds(x - _orientChip.Width, (_top.Height - _orientChip.Height) / 2, _orientChip.Width, _orientChip.Height);
        x -= _orientChip.Width + (int)(22 * dp);
        Right(_zoomInBtn, (int)(40 * dp));
        _zoom.SetBounds(x - (int)(98 * dp), y + (int)(2 * dp), (int)(98 * dp), h - (int)(4 * dp)); x -= (int)(98 * dp) + gap;
        Right(_zoomOutBtn, (int)(40 * dp));
        x -= (int)(14 * dp);
        Right(_redoBtn, (int)(40 * dp));
        Right(_undoBtn, (int)(40 * dp));

        int left = (int)(24 * dp);
        _titleLabel.SetBounds(left, (int)(8 * dp), Math.Max(60, x - left), (int)(30 * dp));
        _subtitle.SetBounds(left, (int)(38 * dp), Math.Max(60, x - left), (int)(20 * dp));
    }

    void LayoutLibrary()
    {
        if (DpiHelper.Scaling) return;
        if (_lib.Width < 100) return;
        float dp = Pal.Dp(this);
        int pad = (int)(16 * dp), w = _lib.Width - pad * 2;
        _libTitle.SetBounds(pad, (int)(14 * dp), w - (int)(60 * dp), (int)(32 * dp));
        _libCount.SetBounds(_lib.Width - pad - (int)(60 * dp), (int)(14 * dp), (int)(60 * dp), (int)(32 * dp));
        _search.SetBounds(pad, (int)(54 * dp), w, (int)(40 * dp));
        int next = (int)(106 * dp);
        if (_saveCustom.Visible)
        {
            _saveCustom.Size = _saveCustom.GetPreferredSize(Size.Empty);
            _saveCustom.Location = new Point(pad, next);
            next += _saveCustom.Height + (int)(8 * dp);
        }
        _libHint.SetBounds(pad, _lib.Height - (int)(30 * dp), w, (int)(22 * dp));
        _grid.SetBounds(0, next, _lib.Width, Math.Max(40, _lib.Height - next - (int)(34 * dp)));
    }

    void LayoutLayersCard()
    {
        if (DpiHelper.Scaling) return;
        if (_layersCard.Width < 100) return;
        float dp = Pal.Dp(this);
        int size = (int)(36 * dp), x = _layersCard.Width - (int)(12 * dp), y = (int)(8 * dp);
        foreach (var b in new[] { _layerDelete, _layerCopy, _layerDown, _layerUp })
        {
            x -= size;
            b.SetBounds(x, y, size, size);
        }
    }

    void PaintBezel(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        Pal.Quality(g);
        float dp = Pal.Dp(this), m = 14 * dp;
        var r = new RectangleF(_canvas.Left - m, _canvas.Top - m, _canvas.Width + m * 2, _canvas.Height + m * 2);
        for (int i = 3; i >= 1; i--)
            Pal.FillRound(g, RectangleF.Inflate(r, i * 3 * dp, i * 3 * dp), 22 * dp + i * 3 * dp, Pal.Alpha(Color.Black, 26));
        Pal.FillRound(g, r, 22 * dp, Pal.Surface);
        Pal.StrokeRound(g, r, 22 * dp, Pal.Alpha(Pal.Outline, 190), Math.Max(1f, dp));
    }

    void CenterCanvas()
    {
        if (DpiHelper.Scaling) return;
        float dp = Pal.Dp(this);
        int margin = (int)(24 * dp);
        int x = Math.Max(margin, (_canvasHost.ClientSize.Width - _canvas.Width) / 2);
        int y = Math.Max(margin, (_canvasHost.ClientSize.Height - _canvas.Height) / 2);
        _canvas.Location = new Point(x, y);
        _canvasHost.Invalidate();
        _canvasHost.AutoScrollMinSize = new Size(_canvas.Width + margin * 2, _canvas.Height + margin * 2);
    }

    // ---------- events ----------

    void WireEvents()
    {
        _canvas.SelectionChanged += OnCanvasSelection;
        _canvas.ThemeEdited += OnCanvasEdited;
        _canvas.DeleteRequested += DeleteSelected;
        _props.Changed += OnPropsChanged;
        _layers.SelectionChanged += OnLayerSelection;
        _layers.VisibilityToggled += OnLayerVisibility;

        _undoBtn.Click += (_, _) => Undo();
        _redoBtn.Click += (_, _) => Redo();
        _saveBtn.Click += (_, _) => Save();
        _openBtn.Click += (_, _) => ShowOpenMenu();
        _moreBtn.Click += (_, _) => ShowMoreMenu();
        _rotateBtn.Click += (_, _) => ShowRotateMenu();
        _zoom.SelectedIndexChanged += (_, _) => ApplyZoom();
        _zoomOutBtn.Click += (_, _) => StepZoom(-1);
        _zoomInBtn.Click += (_, _) => StepZoom(1);
        _layerUp.Click += (_, _) => MoveSelected(1);
        _layerDown.Click += (_, _) => MoveSelected(-1);
        _layerCopy.Click += (_, _) => DuplicateSelected();
        _layerDelete.Click += (_, _) => DeleteSelected();

        _search.TextChanged += (_, _) => RefreshLibrary();
        _grid.ItemClicked += item => { if (item.Tag is LibraryItem li) InsertLibraryItem(li); };
        _grid.ItemRightClicked += OnLibraryRightClick;
        _saveCustom.Click += (_, _) => SaveSelectionAsCustom();

        _pushTimer.Tick += (_, _) => { _pushTimer.Stop(); _livePush(_theme.Clone()); };
        _snapTimer.Tick += async (_, _) => await RefreshSnapshotAsync();
        _commitTimer.Tick += (_, _) => { _commitTimer.Stop(); CommitHistory(); };
        _animTimer.Tick += (_, _) =>
        {
            if (!_closing && !_canvas.Dragging && ThemeRenderer.HasAnimation(_theme)) _canvas.Rerender();
        };
    }

    // ---------- library ----------

    void SelectCategory(LibraryCategory category)
    {
        _category = category;
        for (int i = 0; i < Library.Categories.Count; i++) _railButtons[i].Selected = Library.Categories[i] == category;
        _search.Text = "";
        RefreshLibrary();
    }

    List<LibraryItem> ItemsOf(LibraryCategory c)
    {
        // Custom items change when the user saves or deletes one, so they are never cached.
        if (c.Name == "Custom") return c.Items();
        if (!_itemCache.TryGetValue(c.Name, out var items)) _itemCache[c.Name] = items = c.Items();
        return items;
    }

    void RefreshLibrary()
    {
        string query = _search.Text.Trim();
        List<ThumbItem> items;
        if (query.Length == 0)
        {
            items = ItemsOf(_category).Select(i => new ThumbItem { Name = i.Name, Section = i.Section, Tag = i }).ToList();
            _libTitle.Text = _category.Name.ToUpperInvariant();
            _libHint.Text = _category.Hint + "  -  click a card to add it";
        }
        else
        {
            items = Library.Categories
                .SelectMany(c => ItemsOf(c).Select(i => (c, i)))
                .Where(x => x.i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || x.i.Section.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(x => new ThumbItem { Name = x.i.Name, Section = x.c.Name, Tag = x.i }).ToList();
            _libTitle.Text = "SEARCH";
            _libHint.Text = $"Results for \"{query}\"";
        }
        _libCount.Text = items.Count.ToString();
        bool custom = query.Length == 0 && _category.Name == "Custom";
        _saveCustom.Visible = custom;
        if (custom && items.Count == 0) _libHint.Text = "Nothing saved yet. Select a layer, then Save selected layer";
        LayoutLibrary();
        _grid.SetItems(items);
    }

    void OnLibraryRightClick(ThumbItem item)
    {
        if (item.Tag is not LibraryItem { CustomFile: not null } li) return;
        var menu = UiKit.Menu();
        menu.Items.Add(UiKit.MenuItem("Delete this saved group", () =>
        {
            if (!UiDialog.Confirm(this, "Delete saved group", $"Delete \"{li.Name}\" from your custom library?", "Delete")) return;
            Library.DeleteCustom(li);
            RefreshLibrary();
        }));
        menu.Show(Cursor.Position);
    }

    void SaveSelectionAsCustom()
    {
        var sel = _canvas.Selected;
        if (sel == null)
        {
            UiDialog.Info(this, "Nothing selected", "Select a layer on the canvas or in the Layers list first, then save it to your custom library.");
            return;
        }
        string? name = InputDialog.Prompt(this, "Save to custom library", "Name:", sel.Name);
        if (name == null) return;
        Library.SaveCustom(name, new[] { sel });
        RefreshLibrary();
    }

    static (float x, float y, float w, float h) BoundsOf(IEnumerable<Element> elements)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var e in elements)
        {
            minX = Math.Min(minX, e.X); minY = Math.Min(minY, e.Y);
            maxX = Math.Max(maxX, e.X + e.Width); maxY = Math.Max(maxY, e.Y + e.Height);
        }
        return (minX, minY, maxX - minX, maxY - minY);
    }

    string UniqueName(string name)
    {
        if (_theme.Elements.All(e => e.Name != name)) return name;
        for (int i = 2; ; i++)
            if (_theme.Elements.All(e => e.Name != $"{name} {i}")) return $"{name} {i}";
    }

    /// <summary>Adds the item's elements to the middle of the canvas.</summary>
    void InsertLibraryItem(LibraryItem item, bool promptForMedia = true)
    {
        var elements = item.Build();
        if (elements.Count == 0) return;
        var b = BoundsOf(elements);
        float dx = MathF.Round(_theme.Width / 2f - (b.x + b.w / 2)), dy = MathF.Round(_theme.Height / 2f - (b.y + b.h / 2));
        foreach (var e in elements)
        {
            e.X += dx;
            e.Y += dy;
            e.Name = UniqueName(e.Name);
            _theme.Elements.Add(e);
        }
        if (item.NeedsMedia && promptForMedia)
        {
            string? path = PickMedia();
            if (path != null) foreach (var e in elements.Where(x => x.IsMedia)) e.MediaPath = path;
        }
        _canvas.Rerender();
        RefreshList(elements[^1]);
        MarkChanged();
    }

    string? PickMedia()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Choose an image, GIF or video",
            Filter = "Images and videos|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.m4v;*.mov;*.avi;*.mkv;*.webm;*.wmv|All files|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return null;
        try { return MediaCache.Import(dlg.FileName); }
        catch (Exception ex)
        {
            UiDialog.Info(this, "Could not use that file", ex.Message);
            return null;
        }
    }

    // ---------- zoom ----------

    void StepZoom(int direction)
    {
        int i = _zoom.SelectedIndex;
        if (i < 0) i = 0;
        if (i == 0)
        {
            // From "Fit" step relative to the current effective zoom.
            int current = (int)Math.Round(_canvas.Zoom * 100);
            int target = direction > 0 ? _zoom.Items.Skip(1).Select(x => int.Parse(x.ToString()!.TrimEnd('%'))).FirstOrDefault(v => v > current)
                                       : _zoom.Items.Skip(1).Select(x => int.Parse(x.ToString()!.TrimEnd('%'))).LastOrDefault(v => v < current);
            if (target > 0) _zoom.SelectedItem = target + "%";
            return;
        }
        _zoom.SelectedIndex = Math.Clamp(i + direction, 1, _zoom.Items.Count - 1);
    }

    /// <summary>Applies the zoom box; "Fit" picks the largest zoom that shows the whole canvas.</summary>
    void ApplyZoom()
    {
        string sel = _zoom.SelectedItem?.ToString() ?? "Fit";
        if (sel == "Fit")
        {
            float scale = DeviceDpi / 96f;
            float availW = Math.Max(100, _canvasHost.ClientSize.Width - 48 * scale);
            float availH = Math.Max(100, _canvasHost.ClientSize.Height - 48 * scale);
            _canvas.Zoom = Math.Clamp(Math.Min(availW / (_theme.Width * scale), availH / (_theme.Height * scale)), 0.05f, 1f);
        }
        else if (float.TryParse(sel.TrimEnd('%'), out float pct))
        {
            _canvas.Zoom = pct / 100f;
        }
        CenterCanvas();
    }

    // ---------- layers ----------

    static IconKind IconFor(Element e) => e.Type switch
    {
        ElementType.Text => IconKind.Text,
        ElementType.Media => IconKind.Frame,
        ElementType.Widget => WidgetCatalog.Info(e.Style).Category switch
        {
            WidgetCategory.Gauge => IconKind.Gauge, WidgetCategory.Graph => IconKind.Graph, _ => IconKind.Meter,
        },
        ElementType.ChartCircular or ElementType.ChartPie => IconKind.Gauge,
        ElementType.ChartLinear => IconKind.Meter,
        ElementType.Line => IconKind.Overlay,
        _ => IconKind.Shape,
    };

    void RefreshList(Element? select)
    {
        _syncing = true;
        _listMap = new List<Element?> { null };
        var rows = new List<LayerRow> { new() { Name = "Background", Icon = IconKind.Plate, CanHide = false } };
        for (int i = _theme.Elements.Count - 1; i >= 0; i--)
        {
            var e = _theme.Elements[i];
            _listMap.Add(e);
            rows.Add(new LayerRow { Name = e.Name, Icon = IconFor(e), Visible = e.Visible });
        }
        int idx = select == null ? 0 : _listMap.IndexOf(select);
        _layers.SetRows(rows, idx < 0 ? 0 : idx);
        _syncing = false;
        ApplySelection(select);
    }

    void ApplySelection(Element? e)
    {
        _syncing = true;
        _canvas.Select(e);
        int idx = _listMap.IndexOf(e);
        if (idx >= 0) _layers.SelectedIndex = idx;
        _syncing = false;
        if (e == null) _props.ShowBackground(_theme);
        else _props.ShowElement(e);
    }

    void OnLayerSelection(int index)
    {
        if (_syncing || index < 0 || index >= _listMap.Count) return;
        ApplySelection(_listMap[index]);
    }

    void OnLayerVisibility(int index)
    {
        var e = _listMap[index];
        if (e == null) return;
        e.Visible = !e.Visible;
        _canvas.Rerender();
        var sel = _canvas.Selected;
        RefreshList(sel);
        MarkChanged();
    }

    void OnCanvasSelection(Element? e)
    {
        if (_syncing) return;
        ApplySelection(e);
    }

    // ---------- editing ----------

    void MarkChanged()
    {
        _dirty = _theme.ToJson() != _savedJson;
        UpdateTitle();
        _pushTimer.Stop();
        _pushTimer.Start();
        if (!_restoring)
        {
            _commitTimer.Stop();
            _commitTimer.Start();
        }
    }

    void OnCanvasEdited()
    {
        _props.RefreshGeometry();
        MarkChanged();
    }

    void OnPropsChanged()
    {
        _canvas.Rerender();
        var sel = _canvas.Selected;
        // Keep the layer rows (names, visibility) in sync without disturbing the selection.
        var rows = new List<LayerRow> { new() { Name = "Background", Icon = IconKind.Plate, CanHide = false } };
        for (int i = 1; i < _listMap.Count; i++)
            rows.Add(new LayerRow { Name = _listMap[i]!.Name, Icon = IconFor(_listMap[i]!), Visible = _listMap[i]!.Visible });
        _syncing = true;
        _layers.SetRows(rows, Math.Max(_listMap.IndexOf(sel), 0));
        _syncing = false;
        MarkChanged();
    }

    void AddAndSelect(Element el)
    {
        _theme.Elements.Add(el);
        _canvas.Rerender();
        RefreshList(el);
        MarkChanged();
    }

    void DuplicateSelected()
    {
        var sel = _canvas.Selected;
        if (sel == null) return;
        var copy = sel.Clone();
        copy.Name = UniqueName(sel.Name + " copy");
        copy.X += 20;
        copy.Y += 20;
        _theme.Elements.Insert(_theme.Elements.IndexOf(sel) + 1, copy);
        _canvas.Rerender();
        RefreshList(copy);
        MarkChanged();
    }

    void DeleteSelected()
    {
        var sel = _canvas.Selected;
        if (sel == null) return;
        _theme.Elements.Remove(sel);
        _canvas.Rerender();
        RefreshList(null);
        MarkChanged();
    }

    void MoveSelected(int direction)
    {
        var sel = _canvas.Selected;
        if (sel == null) return;
        int i = _theme.Elements.IndexOf(sel), j = i + direction;
        if (j < 0 || j >= _theme.Elements.Count) return;
        (_theme.Elements[i], _theme.Elements[j]) = (_theme.Elements[j], _theme.Elements[i]);
        _canvas.Rerender();
        RefreshList(sel);
        MarkChanged();
    }

    async Task RefreshSnapshotAsync()
    {
        _snapTimer.Stop();
        try
        {
            var snap = await Task.Run(_engine.GetSnapshot);
            if (!_closing && !IsDisposed)
            {
                _canvas.Snapshot = snap;
                if (!_canvas.Dragging) _canvas.Rerender();
            }
        }
        catch { }
        finally { if (!_closing && !IsDisposed) _snapTimer.Start(); }
    }

    // ---------- undo / redo ----------

    void CommitHistory()
    {
        string json = _theme.ToJson();
        if (json == _lastCommitted) return;
        _undo.Push(_lastCommitted);
        if (_undo.Count > 100) { var keep = _undo.Reverse().Skip(_undo.Count - 100).ToArray(); _undo.Clear(); foreach (var s in keep) _undo.Push(s); }
        _redo.Clear();
        _lastCommitted = json;
        UpdateUndoButtons();
    }

    void UpdateUndoButtons()
    {
        _undoBtn.Enabled = _undo.Count > 0 || _commitTimer.Enabled;
        _redoBtn.Enabled = _redo.Count > 0;
    }

    void Undo()
    {
        if (_commitTimer.Enabled) { _commitTimer.Stop(); CommitHistory(); }
        if (_undo.Count == 0) return;
        _redo.Push(_lastCommitted);
        Restore(_undo.Pop());
    }

    void Redo()
    {
        if (_commitTimer.Enabled) { _commitTimer.Stop(); CommitHistory(); }
        if (_redo.Count == 0) return;
        _undo.Push(_lastCommitted);
        Restore(_redo.Pop());
    }

    void Restore(string json)
    {
        _restoring = true;
        int selectedIndex = _canvas.Selected == null ? -1 : _theme.Elements.IndexOf(_canvas.Selected);
        string name = _theme.Name;
        _theme = Theme.FromJson(json);
        _theme.Name = name;
        _lastCommitted = json;
        _canvas.Theme = _theme;
        ApplyZoom();
        RefreshList(selectedIndex >= 0 && selectedIndex < _theme.Elements.Count ? _theme.Elements[selectedIndex] : null);
        MarkChanged();
        _restoring = false;
        UpdateUndoButtons();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Z: Undo(); return true;
            case Keys.Control | Keys.Y:
            case Keys.Control | Keys.Shift | Keys.Z: Redo(); return true;
            case Keys.Control | Keys.S: Save(); return true;
            case Keys.Control | Keys.D: DuplicateSelected(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- menus ----------

    void ShowOpenMenu()
    {
        var menu = UiKit.Menu();
        foreach (string name in ThemeStore.List())
        {
            string n = name;
            bool current = string.Equals(n, _theme.Name, StringComparison.OrdinalIgnoreCase);
            menu.Items.Add(UiKit.MenuItem((current ? "●  " : "      ") + n, () => OpenTheme(n)));
        }
        UiKit.ShowBelow(menu, _openBtn);
    }

    void ShowMoreMenu()
    {
        var menu = UiKit.Menu();
        menu.Items.Add(UiKit.MenuItem("New blank theme...", NewTheme));
        menu.Items.Add(UiKit.MenuItem("Save as...", SaveAs));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(UiKit.MenuItem("Delete this theme...", DeleteTheme));
        UiKit.ShowBelow(menu, _moreBtn);
    }

    void ShowRotateMenu()
    {
        var menu = UiKit.Menu();
        string to = _theme.IsVertical ? "horizontal" : "vertical";
        menu.Items.Add(UiKit.MenuItem($"Make {to}: turn layout clockwise", () => RotateLayout(true)));
        menu.Items.Add(UiKit.MenuItem($"Make {to}: turn layout counter-clockwise", () => RotateLayout(false)));
        UiKit.ShowBelow(menu, _rotateBtn);
    }

    // ---------- files ----------

    void UpdateTitle()
    {
        _titleLabel.Text = _theme.Name + (_dirty ? "   ●" : "");
        _subtitle.Text = $"Theme editor   |   {_theme.Width} x {_theme.Height} {(_theme.IsVertical ? "vertical" : "horizontal")}   |   {_theme.Elements.Count} layers";
        _orientChip.Text = _theme.IsVertical ? "Vertical" : "Horizontal";
        Text = $"Theme editor - {_theme.Name}{(_dirty ? " *" : "")}";
        LayoutTop();
    }

    bool Save()
    {
        try
        {
            ThemeStore.Save(_theme);
            _savedJson = _theme.ToJson();
            _dirty = false;
            UpdateTitle();
            ThemeSaved?.Invoke(_theme.Name);
            return true;
        }
        catch (Exception ex)
        {
            UiDialog.Info(this, "Could not save the theme", ex.Message);
            return false;
        }
    }

    void SaveAs()
    {
        string? name = InputDialog.Prompt(this, "Save theme as", "Theme name:", _theme.Name);
        if (name == null) return;
        name = ThemeStore.SafeName(name);
        if (ThemeStore.Exists(name) && !string.Equals(name, _theme.Name, StringComparison.OrdinalIgnoreCase)
            && !UiDialog.Confirm(this, "Replace theme?", $"A theme named \"{name}\" already exists. Replace it?", "Replace"))
            return;
        _theme.Name = name;
        Save();
    }

    /// <summary>Skips the "save changes?" question (automated checks edit the theme on purpose).</summary>
    public bool SuppressPrompts { get; set; }

    bool ConfirmDiscard()
    {
        if (!_dirty || SuppressPrompts) return true;
        var r = UiDialog.SaveChanges(this, "Unsaved changes", $"Save your changes to \"{_theme.Name}\" before continuing?");
        if (r == DialogResult.Cancel) return false;
        return r != DialogResult.Yes || Save();
    }

    void LoadIntoEditor(Theme t)
    {
        _theme = t;
        _savedJson = t.ToJson();
        _lastCommitted = _savedJson;
        _undo.Clear();
        _redo.Clear();
        _dirty = false;
        _canvas.Theme = t;
        ApplyZoom();
        RefreshList(null);
        UpdateTitle();
        UpdateUndoButtons();
        _livePush(_theme.Clone());
    }

    void OpenTheme(string name)
    {
        if (!ConfirmDiscard()) return;
        LoadIntoEditor(ThemeStore.Load(name));
    }

    void NewTheme()
    {
        if (!ConfirmDiscard()) return;
        string? name = InputDialog.Prompt(this, "New theme", "Theme name:", "My theme");
        if (name == null) return;
        var choice = UiDialog.Show(this, "Orientation", "Make the new theme vertical (462 x 1920) or horizontal (1920 x 462)?",
            ("Cancel", DialogResult.Cancel, ButtonStyle.Text), ("Horizontal", DialogResult.No, ButtonStyle.Outlined), ("Vertical", DialogResult.Yes, ButtonStyle.Filled));
        if (choice == DialogResult.Cancel) return;
        var t = new Theme { Name = ThemeStore.SafeName(name) };
        if (choice == DialogResult.Yes) (t.Width, t.Height) = (462, 1920);
        LoadIntoEditor(t);
        _dirty = true;
        UpdateTitle();
    }

    void DeleteTheme()
    {
        var names = ThemeStore.List();
        if (!ThemeStore.Exists(_theme.Name))
        {
            UiDialog.Info(this, "Not saved yet", "This theme has not been saved yet, so there is nothing to delete.");
            return;
        }
        if (names.Count <= 1)
        {
            UiDialog.Info(this, "Keep one theme", "You need at least one theme.");
            return;
        }
        if (!UiDialog.Confirm(this, "Delete theme", $"Delete the theme \"{_theme.Name}\" permanently?", "Delete")) return;
        ThemeStore.Delete(_theme.Name);
        string next = ThemeStore.List().First();
        LoadIntoEditor(ThemeStore.Load(next));
        ThemeSaved?.Invoke(next);
    }

    /// <summary>Turns the layout 90 degrees: horizontal becomes vertical and the other way round; every element moves and turns with it.</summary>
    void RotateLayout(bool clockwise)
    {
        string to = _theme.IsVertical ? "horizontal" : "vertical";
        if (!UiDialog.Confirm(this, $"Make this theme {to}?",
                $"The whole layout turns 90 degrees {(clockwise ? "clockwise" : "counter-clockwise")} and every element turns with it, so bars and charts change direction.",
                "Turn layout")) return;
        _theme.RotateLayout(clockwise);
        _canvas.Theme = _theme;
        ApplyZoom();
        RefreshList(null);
        MarkChanged();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
            return;
        }
        _closing = true;
        _pushTimer.Stop();
        _snapTimer.Stop();
        _animTimer.Stop();
        _commitTimer.Stop();
        base.OnFormClosing(e);
    }

    /// <summary>The theme as last saved (or being edited if unsaved).</summary>
    public string CurrentName => _theme.Name;

    /// <summary>Selects a layer by name (used for automated checks).</summary>
    public void SelectByName(string name)
    {
        var el = _theme.Elements.FirstOrDefault(e => e.Name == name);
        if (el != null) RefreshList(el);
    }

    /// <summary>
    /// Inserts every library item, undoes and redoes all of it and renders every preview; returns a report
    /// (used for automated checks, there is no way to click through this much by hand).
    /// </summary>
    public string SelfTest()
    {
        var sb = new System.Text.StringBuilder();
        string original = _theme.ToJson();
        int inserted = 0, failures = 0, thumbs = 0;

        foreach (var cat in Library.Categories.Where(c => c.Name != "Custom"))
        {
            foreach (var item in ItemsOf(cat))
            {
                try
                {
                    int before = _theme.Elements.Count;
                    InsertLibraryItem(item, promptForMedia: false);
                    CommitHistory(); // one undo step per insertion
                    int expected = item.Build().Count;
                    if (_theme.Elements.Count - before != expected)
                    {
                        failures++;
                        sb.AppendLine($"FAIL {cat.Name}/{item.Name}: added {_theme.Elements.Count - before}, expected {expected}");
                    }
                    inserted++;
                    using var thumb = Library.RenderThumb(item, 150, 100);
                    thumbs++;
                }
                catch (Exception ex)
                {
                    failures++;
                    sb.AppendLine($"FAIL {cat.Name}/{item.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        string afterInsert = _theme.ToJson();
        int steps = _undo.Count;
        int layers = _theme.Elements.Count;
        while (_undo.Count > 0) Undo();
        bool undoOk = _theme.ToJson() == original;
        int afterUndo = _theme.Elements.Count;
        while (_redo.Count > 0) Redo();
        bool redoOk = _theme.ToJson() == afterInsert;

        // History keeps the last 100 steps, so check exact undo/redo on a run that stays inside that limit.
        LoadIntoEditor(Theme.FromJson(original));
        string baseline = _theme.ToJson();
        foreach (var item in ItemsOf(Library.Categories[0]).Take(25)) { InsertLibraryItem(item, promptForMedia: false); CommitHistory(); }
        DeleteSelected();
        CommitHistory();
        string edited = _theme.ToJson();
        int smallSteps = _undo.Count;
        while (_undo.Count > 0) Undo();
        bool exactUndo = _theme.ToJson() == baseline;
        while (_redo.Count > 0) Redo();
        bool exactRedo = _theme.ToJson() == edited;
        sb.AppendLine($"exact undo ({smallSteps} steps incl. a delete): {exactUndo}, exact redo: {exactRedo}");

        sb.AppendLine($"items inserted: {inserted}, previews rendered: {thumbs}, failures: {failures}");
        sb.AppendLine($"layers after inserting: {layers}, undo steps: {steps}");
        sb.AppendLine($"undo all -> original theme restored: {undoOk} (layers {afterUndo})");
        sb.AppendLine($"redo all -> final theme restored: {redoOk} (layers {_theme.Elements.Count})");
        return sb.ToString();
    }

    /// <summary>Switches the library to a category by name (used for automated checks).</summary>
    public void ShowCategory(string name)
    {
        var cat = Library.Categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (cat != null) SelectCategory(cat);
    }
}
