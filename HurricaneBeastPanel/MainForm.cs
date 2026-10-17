using System.IO.Ports;
using HurricaneBeastPanel.Ui;

namespace HurricaneBeastPanel;

/// <summary>
/// Main window: a dashboard with the live preview and screen controls, a themes gallery and settings.
/// Closing it keeps the app running in the tray.
/// </summary>
public sealed class MainForm : UiForm
{
    enum Page { Dashboard, Themes, Settings }

    readonly AppSettings _settings = AppSettings.Load();
    readonly PanelEngine _engine;
    readonly NotifyIcon _tray = new();
    readonly ToolTip _tips = UiKit.Tips();
    readonly bool _startMinimized;
    readonly bool _noAutoStart;
    bool _exiting;
    bool _loading = true;
    ThemeEditorForm? _editor;
    Page _page = Page.Dashboard;

    // chrome
    readonly Panel _rail = new() { Dock = DockStyle.Left, Width = 92, BackColor = Pal.SurfaceLo };
    readonly Panel _header = new() { Dock = DockStyle.Top, Height = 92, BackColor = Pal.Bg };
    readonly Panel _pages = new() { Dock = DockStyle.Fill, BackColor = Pal.Bg };
    readonly RailButton _navDashboard = new() { Text = "Dashboard", Icon = IconKind.Home, Size = new Size(88, 70) };
    readonly RailButton _navThemes = new() { Text = "Themes", Icon = IconKind.Themes, Size = new Size(88, 70) };
    readonly RailButton _navSettings = new() { Text = "Settings", Icon = IconKind.Settings, Size = new Size(88, 70) };
    readonly Label _pageTitle = new() { AutoSize = false, ForeColor = Pal.Text, Font = Pal.Ui(20f, FontStyle.Bold), BackColor = Color.Transparent };
    readonly Label _pageSub = new() { AutoSize = false, ForeColor = Pal.TextDim, Font = Pal.Ui(10f), BackColor = Color.Transparent };
    readonly Chip _status = new() { Text = "Stopped", AutoSize = true };
    readonly MButton _toggle = new() { Text = "Start streaming", Glyph = Glyph.Play, Style = ButtonStyle.Filled };

    // dashboard
    readonly Panel _dash = new() { Dock = DockStyle.Fill, BackColor = Pal.Bg };
    readonly Card _previewCard = new() { Title = "Live preview" };
    readonly PreviewBox _preview = new();
    readonly Label _themeName = new() { AutoSize = false, ForeColor = Pal.Text, Font = Pal.Ui(11f, FontStyle.Bold), BackColor = Color.Transparent };
    readonly Label _themeInfo = new() { AutoSize = false, ForeColor = Pal.TextDim, Font = Pal.Ui(9f), BackColor = Color.Transparent };
    readonly MButton _editBtn = new() { Text = "Edit theme", Glyph = Glyph.Edit, Style = ButtonStyle.Filled };
    readonly MButton _browseBtn = new() { Text = "All themes", Glyph = Glyph.Folder, Style = ButtonStyle.Outlined };
    readonly Card _liveCard = new() { Title = "Live readings" };
    readonly StatTile[] _tiles =
    {
        new() { Caption = "CPU temp", Accent = Pal.Primary }, new() { Caption = "CPU load", Accent = Pal.Primary },
        new() { Caption = "RAM used", Accent = Pal.Green }, new() { Caption = "GPU temp", Accent = Pal.Coral },
        new() { Caption = "GPU load", Accent = Pal.Coral }, new() { Caption = "Download", Accent = Pal.Amber },
    };
    readonly Card _screenCard = new() { Title = "Screen" };
    readonly InfoRow _rowModel = new() { Caption = "Model" }, _rowFirmware = new() { Caption = "Firmware" }, _rowResolution = new() { Caption = "Resolution" };
    readonly Label _portLabel = UiText.Label("Port");
    readonly MCombo _port = new() { Width = 200 };
    readonly Label _brightLabel = UiText.Label("Brightness");
    readonly MSlider _brightness = new() { Minimum = 0, Maximum = 100, Suffix = "%" };
    readonly Label _fpsLabel = UiText.Label("Frames per second");
    readonly MSlider _fps = new() { Minimum = 1, Maximum = 30, Suffix = " fps" };
    readonly MButton _restartBtn = new() { Text = "Restart screen", Glyph = Glyph.Refresh, Style = ButtonStyle.Outlined };
    readonly Card _displayCard = new() { Title = "Display" };
    readonly MSwitch _fahrenheit = new() { Text = "Show temperatures in °F" };
    readonly MSwitch _flip = new() { Text = "Flip image upside-down" };
    readonly Label _sensorNote = new() { AutoSize = false, ForeColor = Pal.Amber, Font = Pal.Ui(9f), BackColor = Color.Transparent };

    // themes
    readonly Panel _themesPage = new() { Dock = DockStyle.Fill, BackColor = Pal.Bg };
    readonly ThumbGrid _themeGrid = new() { BackColor = Pal.Bg, CellMinWidth = 300, CellAspect = 0.66f };
    readonly MButton _newThemeBtn = new() { Text = "New theme", Glyph = Glyph.Add, Style = ButtonStyle.Filled };
    readonly Dictionary<string, Theme> _themeCache = new();

    // settings
    readonly Panel _settingsPage = new() { Dock = DockStyle.Fill, BackColor = Pal.Bg };
    readonly Card _startupCard = new() { Title = "Startup" };
    readonly MSwitch _autoConnect = new() { Text = "Start streaming when the app opens" };
    readonly MSwitch _startMin = new() { Text = "Start minimized to the tray" };
    readonly MSwitch _startup = new() { Text = "Start with Windows (runs as administrator)" };
    readonly Card _aboutCard = new() { Title = "About" };
    readonly Label _aboutText = new() { AutoSize = false, ForeColor = Pal.TextDim, Font = Pal.Ui(9.5f), BackColor = Color.Transparent };
    readonly MButton _openFolder = new() { Text = "Open data folder", Glyph = Glyph.Folder, Style = ButtonStyle.Outlined };
    readonly MButton _openLog = new() { Text = "Open log", Style = ButtonStyle.Outlined };
    readonly MButton _openFonts = new() { Text = "Fonts folder", Glyph = Glyph.Folder, Style = ButtonStyle.Outlined };

    readonly System.Windows.Forms.Timer _readings = new() { Interval = 1000 };
    ToolStripMenuItem? _trayToggle;

    public MainForm(bool startMinimized, bool noAutoStart = false)
    {
        _noAutoStart = noAutoStart;
        _startMinimized = !noAutoStart && (startMinimized || _settings.StartMinimized);
        Text = "Hurricane Beast Panel";
        Icon = AppIcon.Small();
        StartPosition = FormStartPosition.CenterScreen;
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1400, 800);
        float scale = DeviceDpi / 96f;
        Size = new Size(Math.Min(1220, (int)(work.Width * 0.9 / scale)), Math.Min(800, (int)(work.Height * 0.92 / scale)));
        MinimumSize = new Size(Math.Min(1000, Size.Width), Math.Min(660, Size.Height));

        ThemeStore.EnsureDefault(_settings);
        _engine = new PanelEngine(_settings);
        _engine.StatusChanged += s => OnUi(() => SetStatus(s));
        _engine.FrameReady += bmp => OnUi(() => _preview.Image = bmp, bmp);
        _engine.DeviceChanged += info => OnUi(() => ShowDevice(info));

        BuildChrome();
        BuildDashboard();
        BuildThemesPage();
        BuildSettingsPage();
        BuildTray();
        LoadSettingsIntoControls();
        WireEvents();
        ShowPage(Page.Dashboard);
        _loading = false;
        DpiHelper.Fit(this);
        _readings.Start();
    }

    // ---------- layout ----------

    void BuildChrome()
    {
        // Rail: logo at the top, navigation below.
        var logo = new PictureBox { Size = new Size(48, 48), Location = new Point(22, 20), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Image = AppIcon.Render(96) };
        _rail.Controls.Add(logo);
        int y = 92;
        foreach (var b in new[] { _navDashboard, _navThemes, _navSettings })
        {
            b.Location = new Point(2, y);
            _rail.Controls.Add(b);
            y += 76;
        }

        _header.Controls.AddRange(new Control[] { _pageTitle, _pageSub, _status, _toggle });
        _header.Resize += (_, _) => LayoutHeader();
        _pages.Controls.AddRange(new Control[] { _dash, _themesPage, _settingsPage });

        // Dock order: the last control added is docked first.
        Controls.Add(_pages);
        Controls.Add(_header);
        Controls.Add(_rail);
    }

    void LayoutHeader()
    {
        if (DpiHelper.Scaling) return;
        float dp = Pal.Dp(this);
        int w = _header.Width, margin = (int)(28 * dp);
        _toggle.Size = _toggle.GetPreferredSize(Size.Empty);
        _toggle.Width += (int)(14 * dp);
        _toggle.Location = new Point(w - margin - _toggle.Width, (_header.Height - _toggle.Height) / 2);
        _status.Size = _status.GetPreferredSize(Size.Empty);
        _status.Location = new Point(_toggle.Left - (int)(16 * dp) - _status.Width, (_header.Height - _status.Height) / 2);
        _pageTitle.SetBounds(margin, (int)(16 * dp), Math.Max(100, _status.Left - margin * 2), (int)(40 * dp));
        _pageSub.SetBounds(margin, (int)(56 * dp), Math.Max(100, _status.Left - margin * 2), (int)(24 * dp));
    }

    void BuildDashboard()
    {
        // Preview card
        _previewCard.Controls.AddRange(new Control[] { _preview, _themeName, _themeInfo, _editBtn, _browseBtn });
        _previewCard.Resize += (_, _) => LayoutPreviewCard();

        // Live readings
        _liveCard.Controls.AddRange(_tiles);
        _liveCard.Resize += (_, _) => LayoutLiveCard();

        // Screen card
        _screenCard.Controls.AddRange(new Control[] { _rowModel, _rowFirmware, _rowResolution, _portLabel, _port, _brightLabel, _brightness, _fpsLabel, _fps, _restartBtn });
        _screenCard.Resize += (_, _) => LayoutScreenCard();

        // Display card
        _displayCard.Controls.AddRange(new Control[] { _fahrenheit, _flip, _sensorNote });
        _displayCard.Resize += (_, _) => LayoutDisplayCard();

        _dash.Controls.AddRange(new Control[] { _previewCard, _liveCard, _screenCard, _displayCard });
        _dash.Resize += (_, _) => LayoutDashboard();
    }

    void LayoutDashboard()
    {
        if (DpiHelper.Scaling) return;
        if (_dash.Width < 200) return;
        float dp = Pal.Dp(this);
        int m = (int)(28 * dp), gap = (int)(18 * dp);
        int rightW = (int)(400 * dp);
        int leftW = Math.Max((int)(300 * dp), _dash.Width - m * 2 - gap - rightW);
        int h = _dash.Height - m - (int)(8 * dp);

        // Left column: preview over live readings.
        int liveH = (int)(236 * dp);
        int previewH = Math.Max((int)(250 * dp), h - gap - liveH);
        _previewCard.SetBounds(m, (int)(4 * dp), leftW, previewH);
        _liveCard.SetBounds(m, _previewCard.Bottom + gap, leftW, h - previewH - gap);

        // Right column: screen over display.
        int displayH = (int)(190 * dp);
        _screenCard.SetBounds(m + leftW + gap, (int)(4 * dp), rightW, h - displayH - gap);
        _displayCard.SetBounds(_screenCard.Left, _screenCard.Bottom + gap, rightW, displayH);
    }

    void LayoutPreviewCard()
    {
        if (DpiHelper.Scaling) return;
        float dp = Pal.Dp(this);
        int pad = (int)(18 * dp), w = _previewCard.Width - pad * 2;
        int footer = (int)(84 * dp);
        _preview.SetBounds(pad, (int)(52 * dp), w, Math.Max(40, _previewCard.Height - (int)(52 * dp) - footer - pad / 2));
        int y = _preview.Bottom + (int)(12 * dp);
        _editBtn.Size = new Size(_editBtn.GetPreferredSize(Size.Empty).Width + (int)(12 * dp), (int)(38 * dp));
        _browseBtn.Size = new Size(_browseBtn.GetPreferredSize(Size.Empty).Width + (int)(12 * dp), (int)(38 * dp));
        _editBtn.Location = new Point(_previewCard.Width - pad - _editBtn.Width, y + (int)(2 * dp));
        _browseBtn.Location = new Point(_editBtn.Left - (int)(10 * dp) - _browseBtn.Width, y + (int)(2 * dp));
        _themeName.SetBounds(pad, y, Math.Max(80, _browseBtn.Left - pad - (int)(10 * dp)), (int)(26 * dp));
        _themeInfo.SetBounds(pad, y + (int)(28 * dp), Math.Max(80, _browseBtn.Left - pad - (int)(10 * dp)), (int)(22 * dp));
    }

    void LayoutLiveCard()
    {
        if (DpiHelper.Scaling) return;
        float dp = Pal.Dp(this);
        int pad = (int)(18 * dp), gap = (int)(12 * dp), cols = 3;
        int tileW = (_liveCard.Width - pad * 2 - gap * (cols - 1)) / cols;
        int top = (int)(52 * dp);
        int tileH = Math.Min((int)(92 * dp), (_liveCard.Height - top - pad - gap) / 2);
        for (int i = 0; i < _tiles.Length; i++)
            _tiles[i].SetBounds(pad + i % cols * (tileW + gap), top + i / cols * (tileH + gap), tileW, tileH);
    }

    void LayoutScreenCard()
    {
        if (DpiHelper.Scaling) return;
        float dp = Pal.Dp(this);
        int pad = (int)(20 * dp), w = _screenCard.Width - pad * 2, y = (int)(52 * dp), rowH = (int)(36 * dp);
        foreach (var r in new[] { _rowModel, _rowFirmware, _rowResolution })
        {
            r.SetBounds(pad, y, w, rowH);
            y += rowH;
        }
        y += (int)(14 * dp);
        _portLabel.Location = new Point(pad, y);
        _port.SetBounds(pad, y + (int)(24 * dp), w, (int)(38 * dp));
        y += (int)(78 * dp);
        _brightLabel.Location = new Point(pad, y);
        _brightness.SetBounds(pad - (int)(6 * dp), y + (int)(22 * dp), w + (int)(6 * dp), (int)(34 * dp));
        y += (int)(70 * dp);
        _fpsLabel.Location = new Point(pad, y);
        _fps.SetBounds(pad - (int)(6 * dp), y + (int)(22 * dp), w + (int)(6 * dp), (int)(34 * dp));
        // Restart sits in the card header, next to the title.
        _restartBtn.Size = new Size(_restartBtn.GetPreferredSize(Size.Empty).Width + (int)(8 * dp), (int)(34 * dp));
        _restartBtn.Location = new Point(_screenCard.Width - pad + (int)(4 * dp) - _restartBtn.Width, (int)(10 * dp));
    }

    void LayoutDisplayCard()
    {
        if (DpiHelper.Scaling) return;
        float dp = Pal.Dp(this);
        int pad = (int)(20 * dp), w = _displayCard.Width - pad * 2;
        _fahrenheit.SetBounds(pad, (int)(52 * dp), w, (int)(34 * dp));
        _flip.SetBounds(pad, (int)(92 * dp), w, (int)(34 * dp));
        _sensorNote.SetBounds(pad, (int)(132 * dp), w, (int)(44 * dp));
    }

    void BuildThemesPage()
    {
        _themesPage.Controls.Add(_themeGrid);
        _themesPage.Controls.Add(_newThemeBtn);
        _themesPage.Resize += (_, _) =>
        {
            if (DpiHelper.Scaling) return;
            float dp = Pal.Dp(this);
            int m = (int)(28 * dp);
            _newThemeBtn.Size = new Size(_newThemeBtn.GetPreferredSize(Size.Empty).Width + (int)(12 * dp), (int)(40 * dp));
            _newThemeBtn.Location = new Point(m, (int)(4 * dp));
            _themeGrid.SetBounds(m - (int)(10 * dp), (int)(56 * dp), _themesPage.Width - m * 2 + (int)(20 * dp), _themesPage.Height - (int)(64 * dp));
        };
        _themeGrid.ImageProvider = (item, w, h) =>
        {
            if (item.Tag is not string name) return null;
            var theme = ThemeFor(name);
            float scale = Math.Min(w / (float)theme.Width, h / (float)theme.Height);
            int rw = Math.Max(2, (int)(theme.Width * scale)), rh = Math.Max(2, (int)(theme.Height * scale));
            using var rendered = ThemeRenderer.Render(theme, Library.Demo, _settings, rw, rh, preview: true);
            var result = new Bitmap(w, h);
            using var g = Graphics.FromImage(result);
            g.Clear(Color.Black);
            g.DrawImage(rendered, (w - rw) / 2, (h - rh) / 2, rw, rh);
            return result;
        };
    }

    Theme ThemeFor(string name)
    {
        if (!_themeCache.TryGetValue(name, out var t)) _themeCache[name] = t = ThemeStore.Load(name);
        return t;
    }

    void BuildSettingsPage()
    {
        _startupCard.Controls.AddRange(new Control[] { _autoConnect, _startMin, _startup });
        _startupCard.Resize += (_, _) =>
        {
            if (DpiHelper.Scaling) return;
            float dp = Pal.Dp(this);
            int pad = (int)(20 * dp), w = _startupCard.Width - pad * 2, y = (int)(52 * dp);
            foreach (var s in new[] { _autoConnect, _startMin, _startup })
            {
                s.SetBounds(pad, y, w, (int)(36 * dp));
                y += (int)(42 * dp);
            }
        };
        _aboutText.Text = "Hurricane Beast Panel streams a live dashboard to the Hurricane Beast USB screen.\r\n" +
                          "Themes, settings and logs are kept in your AppData folder. Extra fonts: put .ttf/.otf files in the fonts folder.";
        _aboutCard.Controls.AddRange(new Control[] { _aboutText, _openFolder, _openLog, _openFonts });
        _aboutCard.Resize += (_, _) =>
        {
            if (DpiHelper.Scaling) return;
            float dp = Pal.Dp(this);
            int pad = (int)(20 * dp), w = _aboutCard.Width - pad * 2;
            _aboutText.SetBounds(pad, (int)(52 * dp), w, (int)(56 * dp));
            _openFolder.Size = new Size(_openFolder.GetPreferredSize(Size.Empty).Width + (int)(12 * dp), (int)(38 * dp));
            _openLog.Size = new Size(_openLog.GetPreferredSize(Size.Empty).Width + (int)(12 * dp), (int)(38 * dp));
            _openFolder.Location = new Point(pad, (int)(116 * dp));
            _openLog.Location = new Point(_openFolder.Right + (int)(10 * dp), (int)(116 * dp));
            _openFonts.Size = new Size(_openFonts.GetPreferredSize(Size.Empty).Width + (int)(12 * dp), (int)(38 * dp));
            _openFonts.Location = new Point(_openLog.Right + (int)(10 * dp), (int)(116 * dp));
        };
        _settingsPage.Controls.AddRange(new Control[] { _startupCard, _aboutCard });
        _settingsPage.Resize += (_, _) =>
        {
            if (DpiHelper.Scaling) return;
            float dp = Pal.Dp(this);
            int m = (int)(28 * dp), gap = (int)(18 * dp), w = Math.Min((int)(640 * dp), _settingsPage.Width - m * 2);
            _startupCard.SetBounds(m, (int)(4 * dp), w, (int)(190 * dp));
            _aboutCard.SetBounds(m, _startupCard.Bottom + gap, w, (int)(180 * dp));
        };
    }

    void BuildTray()
    {
        var menu = UiKit.Menu();
        menu.Items.Add(UiKit.MenuItem("Open", ShowWindow));
        _trayToggle = UiKit.MenuItem("Start streaming", () => _ = ToggleAsync());
        menu.Items.Add(_trayToggle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(UiKit.MenuItem("Exit", ExitApp));

        _tray.Icon = AppIcon.Small();
        _tray.Text = "Hurricane Beast Panel";
        _tray.ContextMenuStrip = menu;
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    // ---------- settings <-> controls ----------

    void LoadSettingsIntoControls()
    {
        RefreshPorts();
        RefreshThemes();
        _brightness.Value = Math.Clamp(_settings.Brightness, 0, 100);
        _fps.Value = Math.Clamp(_settings.SendFps, 1, 30);
        _fahrenheit.Checked = _settings.Fahrenheit;
        _flip.Checked = _settings.Flip180;
        _autoConnect.Checked = _settings.AutoConnect;
        _startMin.Checked = _settings.StartMinimized;
        _startup.Checked = StartupTask.IsEnabled();
        if (_engine.SensorError != null)
            _sensorNote.Text = "Sensor driver problem: " + _engine.SensorError;
        else if (!IsAdmin())
            _sensorNote.Text = "Not running as administrator, so CPU temperature and power will be missing.";
        UpdateThemeCard();
    }

    static bool IsAdmin()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    void RefreshPorts()
    {
        _port.Items.Clear();
        _port.Items.Add("Auto-detect");
        foreach (string p in SerialPort.GetPortNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) _port.Items.Add(p);
        int idx = string.IsNullOrWhiteSpace(_settings.Port) ? 0 : _port.Items.IndexOf(_settings.Port);
        _port.SelectedIndex = idx < 0 ? 0 : idx;
    }

    void RefreshThemes()
    {
        var names = ThemeStore.List();
        if (!names.Any(n => string.Equals(n, _settings.ThemeName, StringComparison.OrdinalIgnoreCase)))
        {
            _settings.ThemeName = names[0];
            _settings.Save();
        }
        _themeCache.Clear();
        var items = names.Select(n => new ThumbItem
        {
            Name = n, Tag = n,
            Badge = string.Equals(n, _settings.ThemeName, StringComparison.OrdinalIgnoreCase) ? "ACTIVE" : "",
        }).ToList();
        _themeGrid.SetItems(items, keepScroll: true);
        UpdateThemeCard();
    }

    void UpdateThemeCard()
    {
        _themeName.Text = _settings.ThemeName;
        var t = ThemeFor(_settings.ThemeName);
        _themeInfo.Text = $"{t.Width} x {t.Height} {(t.IsVertical ? "vertical" : "horizontal")}   |   {t.Elements.Count} layers";
    }

    void ApplyTheme() => _engine.SetTheme(ThemeStore.Load(_settings.ThemeName));

    void SetActiveTheme(string name)
    {
        _settings.ThemeName = name;
        _settings.Save();
        ApplyTheme();
        RefreshThemes();
    }

    void WireEvents()
    {
        _navDashboard.Click += (_, _) => ShowPage(Page.Dashboard);
        _navThemes.Click += (_, _) => ShowPage(Page.Themes);
        _navSettings.Click += (_, _) => ShowPage(Page.Settings);

        _port.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.Port = _port.SelectedIndex <= 0 ? "" : _port.SelectedItem!.ToString()!;
            _settings.Save();
        };
        _port.MouseEnter += (_, _) => { if (!_port.Focused) RefreshPortsKeepSelection(); };
        _brightness.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.Brightness = _brightness.Value;
            _settings.Save();
            _engine.ApplyBrightness();
        };
        _fps.ValueChanged += (_, _) => { if (!_loading) { _settings.SendFps = _fps.Value; _settings.Save(); } };
        _fahrenheit.CheckedChanged += (_, _) => { if (!_loading) { _settings.Fahrenheit = _fahrenheit.Checked; _settings.Save(); } };
        _flip.CheckedChanged += (_, _) => { if (!_loading) { _settings.Flip180 = _flip.Checked; _settings.Save(); } };
        _autoConnect.CheckedChanged += (_, _) => { if (!_loading) { _settings.AutoConnect = _autoConnect.Checked; _settings.Save(); } };
        _startMin.CheckedChanged += (_, _) => { if (!_loading) { _settings.StartMinimized = _startMin.Checked; _settings.Save(); } };
        _startup.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            bool ok = _startup.Checked ? StartupTask.Enable(Environment.ProcessPath!) : StartupTask.Disable();
            if (!ok)
            {
                UiDialog.Info(this, "Could not change the startup task", "Run the app as administrator and try again.");
                _loading = true;
                _startup.Checked = StartupTask.IsEnabled();
                _loading = false;
            }
        };

        _toggle.Click += async (_, _) => await ToggleAsync();
        _restartBtn.Click += (_, _) => _engine.RestartScreen();
        _tips.SetToolTip(_restartBtn, "Restarts the screen itself. Use this if it goes blank; no need to unplug it.");
        _editBtn.Click += (_, _) => OpenEditor();
        _browseBtn.Click += (_, _) => ShowPage(Page.Themes);
        _newThemeBtn.Click += (_, _) => NewTheme();
        _themeGrid.ItemClicked += item => { if (item.Tag is string n) SetActiveTheme(n); };
        _themeGrid.ItemRightClicked += OnThemeRightClick;
        _openFolder.Click += (_, _) => OpenPath(AppSettings.Dir);
        _openLog.Click += (_, _) => OpenPath(Path.Combine(AppSettings.Dir, "log.txt"));
        _openFonts.Click += (_, _) => { FontStore.EnsureLoaded(); OpenPath(FontStore.Dir); };
        _tips.SetToolTip(_openFonts, "Drop .ttf or .otf font files here, then restart the app. They appear in the font list of the theme editor.");
        _readings.Tick += (_, _) => RefreshReadings();
    }

    void RefreshPortsKeepSelection()
    {
        string current = _port.SelectedItem?.ToString() ?? "Auto-detect";
        bool was = _loading;
        _loading = true;
        RefreshPorts();
        int i = _port.Items.IndexOf(current);
        if (i >= 0) _port.SelectedIndex = i;
        _loading = was;
    }

    static void OpenPath(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }
    }

    // ---------- pages ----------

    void ShowPage(Page page)
    {
        _page = page;
        _dash.Visible = page == Page.Dashboard;
        _themesPage.Visible = page == Page.Themes;
        _settingsPage.Visible = page == Page.Settings;
        _navDashboard.Selected = page == Page.Dashboard;
        _navThemes.Selected = page == Page.Themes;
        _navSettings.Selected = page == Page.Settings;
        (_pageTitle.Text, _pageSub.Text) = page switch
        {
            Page.Dashboard => ("Dashboard", "Your screen at a glance"),
            Page.Themes => ("Themes", "Click a theme to show it on the screen. Right-click for more."),
            _ => ("Settings", "Startup and app options"),
        };
        if (page == Page.Themes) RefreshThemes();
        LayoutHeader();
        _dash.PerformLayout();
    }

    // ---------- themes ----------

    void OnThemeRightClick(ThumbItem item)
    {
        if (item.Tag is not string name) return;
        var menu = UiKit.Menu();
        menu.Items.Add(UiKit.MenuItem("Show on the screen", () => SetActiveTheme(name)));
        menu.Items.Add(UiKit.MenuItem("Edit...", () => { SetActiveTheme(name); OpenEditor(); }));
        menu.Items.Add(UiKit.MenuItem("Duplicate", () => DuplicateTheme(name)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(UiKit.MenuItem("Delete...", () => DeleteTheme(name)));
        menu.Show(Cursor.Position);
    }

    void NewTheme()
    {
        string? name = InputDialog.Prompt(this, "New theme", "Theme name:", "My theme");
        if (name == null) return;
        var choice = UiDialog.Show(this, "Orientation", "Make the new theme vertical (462 x 1920) or horizontal (1920 x 462)?",
            ("Cancel", DialogResult.Cancel, ButtonStyle.Text), ("Horizontal", DialogResult.No, ButtonStyle.Outlined), ("Vertical", DialogResult.Yes, ButtonStyle.Filled));
        if (choice == DialogResult.Cancel) return;
        name = ThemeStore.SafeName(name);
        if (ThemeStore.Exists(name) && !UiDialog.Confirm(this, "Replace theme?", $"A theme named \"{name}\" already exists. Replace it?", "Replace")) return;
        var t = new Theme { Name = name };
        if (choice == DialogResult.Yes) (t.Width, t.Height) = (462, 1920);
        ThemeStore.Save(t);
        SetActiveTheme(name);
        OpenEditor();
    }

    void DuplicateTheme(string name)
    {
        string? copy = InputDialog.Prompt(this, "Duplicate theme", "Name of the copy:", name + " copy");
        if (copy == null) return;
        var t = ThemeStore.Load(name);
        t.Name = ThemeStore.SafeName(copy);
        ThemeStore.Save(t);
        RefreshThemes();
    }

    void DeleteTheme(string name)
    {
        if (ThemeStore.List().Count <= 1)
        {
            UiDialog.Info(this, "Keep one theme", "You need at least one theme.");
            return;
        }
        if (!UiDialog.Confirm(this, "Delete theme", $"Delete the theme \"{name}\" permanently?", "Delete")) return;
        ThemeStore.Delete(name);
        if (string.Equals(name, _settings.ThemeName, StringComparison.OrdinalIgnoreCase))
        {
            _settings.ThemeName = ThemeStore.List().First();
            _settings.Save();
            ApplyTheme();
        }
        RefreshThemes();
    }

    void OpenEditor()
    {
        if (_editor != null && !_editor.IsDisposed)
        {
            _editor.Activate();
            return;
        }
        _editor = new ThemeEditorForm(ThemeStore.Load(_settings.ThemeName), _engine, _settings, t => _engine.SetTheme(t));
        _editor.ThemeSaved += name =>
        {
            _settings.ThemeName = name;
            _settings.Save();
            RefreshThemes();
        };
        _editor.FormClosed += (_, _) =>
        {
            _editor = null;
            RefreshThemes();
            ApplyTheme(); // drop any unsaved live preview
        };
        _editor.Show(this);
    }

    // ---------- behaviour ----------

    async Task ToggleAsync()
    {
        _toggle.Enabled = false;
        if (_engine.Running) await _engine.StopAsync();
        else _engine.Start();
        UpdateToggle();
        _toggle.Enabled = true;
    }

    void UpdateToggle()
    {
        bool running = _engine.Running;
        _toggle.Text = running ? "Stop streaming" : "Start streaming";
        _toggle.Glyph = running ? Glyph.Stop : Glyph.Play;
        _toggle.Style = running ? ButtonStyle.Tonal : ButtonStyle.Filled;
        if (_trayToggle != null) _trayToggle.Text = running ? "Stop streaming" : "Start streaming";
        LayoutHeader();
    }

    void SetStatus(string text)
    {
        _status.Text = text.Length > 54 ? text[..54] + "..." : text;
        string lower = text.ToLowerInvariant();
        _status.Dot = lower.StartsWith("streaming") ? Pal.Green
            : lower.Contains("busy") || lower.Contains("lost") || lower.Contains("error") ? Pal.Red
            : lower.Contains("not found") || lower.Contains("connecting") || lower.Contains("looking") || lower.Contains("restart") ? Pal.Amber
            : Pal.TextFaint;
        _status.Invalidate();
        _tray.Text = text.Length > 60 ? text[..60] : text;
        UpdateToggle();
    }

    void ShowDevice(ScreenInfo? info)
    {
        _rowModel.Value = info?.Model ?? "-";
        _rowFirmware.Value = info == null ? "-" : info.Version.ToString("0.0");
        _rowResolution.Value = info == null ? "-" : $"{info.Width} x {info.Height}";
        if (info == null) _preview.Placeholder = "Screen not connected";
    }

    void RefreshReadings()
    {
        if (!Visible || _page != Page.Dashboard) return;
        try
        {
            var s = _engine.GetSnapshot();
            string F(float? v, string suffix, string format = "0") => v.HasValue ? v.Value.ToString(format) + suffix : "--";
            string unit = _settings.Fahrenheit ? "°F" : "°C";
            float? Temp(float? c) => c.HasValue && _settings.Fahrenheit ? c * 9 / 5 + 32 : c;
            _tiles[0].Value = F(Temp(s.CpuTemp), unit);
            _tiles[1].Value = F(s.CpuLoad, " %");
            _tiles[2].Value = s.RamUsedGb.HasValue && s.RamTotalGb.HasValue ? $"{s.RamUsedGb:0.0} / {s.RamTotalGb:0} GB" : F(s.RamLoad, " %");
            _tiles[3].Value = F(Temp(s.GpuTemp), unit);
            _tiles[4].Value = F(s.GpuLoad, " %");
            _tiles[5].Value = (s.NetDownBps * 8 / 1_000_000).ToString("0.0") + " Mbps";
        }
        catch { /* sensors may be busy */ }
    }

    /// <summary>Runs on the UI thread; disposes the bitmap if the form is already gone.</summary>
    void OnUi(Action action, IDisposable? disposeIfDropped = null)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            disposeIfDropped?.Dispose();
            return;
        }
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { disposeIfDropped?.Dispose(); }
    }

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    void ExitApp()
    {
        _exiting = true;
        Close();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Keep the window hidden on first show when starting minimized.
        if (_startMinimized && !IsHandleCreated)
        {
            CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateToggle();
        LayoutHeader();
        _dash.PerformLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_settings.AutoConnect && !_noAutoStart && !_engine.Running)
        {
            _engine.Start();
            UpdateToggle();
        }
    }

    /// <summary>Switches to a page by name (used for automated checks).</summary>
    public void ShowPageByName(string name)
    {
        if (Enum.TryParse<Page>(name, true, out var page)) ShowPage(page);
    }

    /// <summary>Draws the active theme into the preview without a screen connected (used for automated checks).</summary>
    public void ShowDemoFrame()
    {
        var t = ThemeStore.Load(_settings.ThemeName);
        _preview.Image = ThemeRenderer.Render(t, Library.Demo, _settings, t.Width, t.Height, preview: true);
        RefreshReadings();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _readings.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _engine.Dispose();
        base.OnFormClosing(e);
    }
}
