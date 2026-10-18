using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace HurricaneBeastPanel;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Headless check of the dashboard layout: --render-test <out.png>
        int rt = Array.IndexOf(args, "--render-test");
        if (rt >= 0 && rt + 1 < args.Length)
            return RenderTest(args[rt + 1], rt + 2 < args.Length ? args[rt + 2] : null);

        // --gallery <category: Gauge|Graph|Meter> <out.png>: renders every widget style of that category.
        int gal = Array.IndexOf(args, "--gallery");
        if (gal >= 0 && gal + 2 < args.Length && Enum.TryParse<WidgetCategory>(args[gal + 1], true, out var category))
        {
            var galleryTheme = DevTools.Gallery(category);
            using var sensors = new SensorService();
            sensors.Poll();
            Thread.Sleep(1200);
            var snapshot = sensors.Poll();
            SensorHistory.Push(snapshot);
            using var image = ThemeRenderer.Render(galleryTheme, snapshot, AppSettings.Load(), galleryTheme.Width, galleryTheme.Height, preview: true);
            image.Save(args[gal + 2], System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }

        // --weather-gallery <out.png>: every weather picture plus the ready-made weather blocks, using live weather.
        int wg = Array.IndexOf(args, "--weather-gallery");
        if (wg >= 0 && wg + 1 < args.Length)
        {
            var wt = new Theme { Name = "Weather test", Width = 1920, Height = 924, Background = FillSpec.Solid("#FF0B0E1A") };
            var items = Library.Categories.First(c => c.Name == "Weather").Items();
            float x = 10, y = 10;
            foreach (var item in items)
            {
                if (x + item.Width > 1900) { x = 10; y += 250; }
                foreach (var el in item.Build()) { el.X += x; el.Y += y; wt.Elements.Add(el); }
                x += item.Width + 20;
            }
            var ws = AppSettings.Load();
            using var wsensors = new SensorService();
            wsensors.Poll();
            Thread.Sleep(300);
            WeatherService.Touch(ws.WeatherLocation);
            for (int i = 0; i < 40 && WeatherService.Current == null; i++) Thread.Sleep(250);
            var wsnap = wsensors.Poll();
            using var wimg = ThemeRenderer.Render(wt, wsnap, ws, wt.Width, wt.Height);
            wimg.Save(args[wg + 1], System.Drawing.Imaging.ImageFormat.Png);
            File.WriteAllText(args[wg + 1] + ".txt", wsnap.Weather == null ? "no weather data" : wsnap.Weather.ToString());
            return 0;
        }

        // --picker-test <out.png>: picture of the colour picker dialog.
        int pk = Array.IndexOf(args, "--picker-test");
        if (pk >= 0 && pk + 1 < args.Length)
        {
            using var picker = ColorPickerDialog.CreateForTest(Color.FromArgb(200, 88, 35, 205));
            picker.Show();
            Application.DoEvents();
            using var pbmp = new Bitmap(picker.Width, picker.Height);
            picker.DrawToBitmap(pbmp, new Rectangle(0, 0, picker.Width, picker.Height));
            pbmp.Save(args[pk + 1], System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }

        // --fps-test <out.txt> [@themeKey]: times drawing + JPEG encoding of an animated theme to see what frame rates the PC can sustain.
        int ft = Array.IndexOf(args, "--fps-test");
        if (ft >= 0 && ft + 1 < args.Length)
        {
            var fs = AppSettings.Load();
            var ftheme = ExtraThemes.ByKey(ft + 2 < args.Length && args[ft + 2].StartsWith("@") ? args[ft + 2][1..] : "rainbowh");
            var rain = Element.Create(ElementType.Weather, 1, 200, 200);
            rain.Name = "Rain test"; rain.Bind = "rain"; rain.X = 1650; rain.Y = 100; rain.Width = 200; rain.Height = 200;
            ftheme.Elements.Add(rain); // makes the theme animated, like a GIF would
            using var fsensors = new SensorService();
            fsensors.Poll(); Thread.Sleep(800);
            var fsnap = fsensors.Poll();
            var report = new System.Text.StringBuilder();
            report.AppendLine($"theme {ftheme.Name} {ftheme.Width}x{ftheme.Height}, animated={ThemeRenderer.HasAnimation(ftheme)}");
            int q = 90; long total = 0; const int frames = 90;
            for (int i = 0; i < 5; i++) { using var warm = ThemeRenderer.Render(ftheme, fsnap, fs, ftheme.Width, ftheme.Height); }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                using var f = ThemeRenderer.Render(ftheme, fsnap, fs, ftheme.Width, ftheme.Height);
                f.RotateFlip(RotateFlipType.Rotate270FlipNone);
                total += PanelEngine.EncodeJpeg(f, 90 * 1024, ref q).Length;
                Thread.Sleep(0);
            }
            double ms = watch.Elapsed.TotalMilliseconds / frames, kb = total / (double)frames / 1024;
            report.AppendLine($"draw + encode: {ms:0.0} ms per frame  ->  this PC can make about {1000 / ms:0} frames per second");
            report.AppendLine($"frame size: {kb:0.0} KB");
            foreach (int rate in new[] { 1, 5, 10, 15, 20, 30 })
                report.AppendLine($"at {rate,2} fps setting: needs {ms * rate / 10:0}% of one CPU core, writes {kb * rate:0} KB/s to the screen{(ms > 1000.0 / rate ? "   <- PC too slow, real rate is lower" : "")}");
            File.WriteAllText(args[ft + 1], report.ToString());

            // A theme to try on the real screen: moving rain, snow and storm pictures plus a clock that counts milliseconds.
            var test = new Theme { Name = "FPS Test", Width = 1920, Height = 462, Background = FillSpec.Solid("#FF05070D") };
            float x = 40;
            foreach (string kind in new[] { "rain", "snow", "storm" })
            {
                var pic = Element.Create(ElementType.Weather, 1, 400, 400);
                pic.Name = "Weather " + kind; pic.Bind = kind; pic.X = x; pic.Y = 30; pic.Width = 400; pic.Height = 400;
                test.Elements.Add(pic);
                x += 420;
            }
            var clock = Theme.TextEl("Milliseconds", "{time:ss.fff}", 1300, 90, 580, 150, 130, true, "#FF7CF0FF", HAlign.Center);
            test.Elements.Add(clock);
            test.Elements.Add(Theme.TextEl("Hint", "Fast = smooth motion, digits blur", 1300, 260, 580, 50, 26, false, "#FF8C97AC", HAlign.Center));
            ThemeStore.Save(test);
            return 0;
        }

        // --render-demo <out.png> <theme name or @key>: draws a theme with sample sensor values (for screenshots).
        int rd = Array.IndexOf(args, "--render-demo");
        if (rd >= 0 && rd + 2 < args.Length)
        {
            var ds = AppSettings.Load();
            ThemeStore.EnsureDefault();
            var dtheme = args[rd + 2].StartsWith("@") ? ExtraThemes.ByKey(args[rd + 2][1..]) : ThemeStore.Load(args[rd + 2]);
            var demo = Library.Demo;
            float cpu0 = demo.CpuTemp ?? 54, gpu0 = demo.GpuTemp ?? 61, load0 = demo.CpuLoad ?? 38, gl0 = demo.GpuLoad ?? 72;
            var rng = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                // a lively but believable history for the graph elements
                demo.CpuTemp = cpu0 + 6 * MathF.Sin(i / 14f) + (float)rng.NextDouble() * 3;
                demo.GpuTemp = gpu0 + 5 * MathF.Sin(i / 9f + 1) + (float)rng.NextDouble() * 2;
                demo.CpuLoad = Math.Clamp(load0 + 25 * MathF.Sin(i / 6f) + (float)rng.NextDouble() * 12, 2, 100);
                demo.GpuLoad = Math.Clamp(gl0 + 20 * MathF.Sin(i / 8f + 2) + (float)rng.NextDouble() * 8, 2, 100);
                SensorHistory.Push(demo);
            }
            demo.CpuTemp = cpu0; demo.GpuTemp = gpu0; demo.CpuLoad = load0; demo.GpuLoad = gl0;
            ThemeRenderer.Render(dtheme, Library.Demo, ds, dtheme.Width, dtheme.Height).Dispose();
            Thread.Sleep(700);
            using var dimg = ThemeRenderer.Render(dtheme, Library.Demo, ds, dtheme.Width, dtheme.Height);
            dimg.Save(args[rd + 1], System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }

        int dg = Array.IndexOf(args, "--date-gallery");
        if (dg >= 0 && dg + 1 < args.Length)
        {
            var dates = DevTools.DateGallery();
            using var datesImage = ThemeRenderer.Render(dates, new SensorSnapshot(), AppSettings.Load(), dates.Width, dates.Height);
            datesImage.Save(args[dg + 1], System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }

        // --dialog-test out.png: saves a picture of the message dialogs (no clicking needed to check their layout).
        int dt = Array.IndexOf(args, "--dialog-test");
        if (dt >= 0 && dt + 1 < args.Length)
        {
            var dialogs = new[]
            {
                HurricaneBeastPanel.Ui.UiDialog.Build("Orientation", "Make the new theme vertical (462 x 1920) or horizontal (1920 x 462)?",
                    new[] { ("Cancel", DialogResult.Cancel, HurricaneBeastPanel.Ui.ButtonStyle.Text), ("Horizontal", DialogResult.No, HurricaneBeastPanel.Ui.ButtonStyle.Outlined), ("Vertical", DialogResult.Yes, HurricaneBeastPanel.Ui.ButtonStyle.Filled) }),
                HurricaneBeastPanel.Ui.UiDialog.Build("Unsaved changes", "Save your changes to \"Default\" before continuing? Anything you do not save will be lost when you open another theme, and this longer sentence checks that the text wraps onto a second line.",
                    new[] { ("Cancel", DialogResult.Cancel, HurricaneBeastPanel.Ui.ButtonStyle.Text), ("Don't save", DialogResult.No, HurricaneBeastPanel.Ui.ButtonStyle.Outlined), ("Save", DialogResult.Yes, HurricaneBeastPanel.Ui.ButtonStyle.Filled) }),
            };
            int shotNo = 0;
            foreach (var d in dialogs)
            {
                d.Show();
                Application.DoEvents();
                using var bmp = new Bitmap(d.Width, d.Height);
                d.DrawToBitmap(bmp, new Rectangle(0, 0, d.Width, d.Height));
                bmp.Save(args[dt + 1].Replace(".png", $"_{++shotNo}.png"), System.Drawing.Imaging.ImageFormat.Png);
                d.Close();
            }
            return 0;
        }

        if (args.Contains("--save-neon"))
        {
            ThemeStore.Save(NeonTheme.Create());
            foreach (var extra in ExtraThemes.All()) ThemeStore.Save(extra);
            return 0;
        }

        int mi = Array.IndexOf(args, "--make-icon");
        if (mi >= 0 && mi + 1 < args.Length)
        {
            HurricaneBeastPanel.Ui.AppIcon.WriteIco(args[mi + 1]);
            return 0;
        }

        int mk = Array.IndexOf(args, "--make-test-media");
        if (mk >= 0 && mk + 1 < args.Length)
        {
            DevTools.MakeTestMedia(args[mk + 1]);
            return 0;
        }

        // Opens only the main window without connecting to the screen: --main-only [--page Themes] [--shot out.png]
        if (args.Contains("--main-only"))
        {
            ThemeStore.EnsureDefault();
            var mainWindow = new MainForm(false, noAutoStart: true) { TopMost = args.Contains("--shot") };
            int pg = Array.IndexOf(args, "--page");
            mainWindow.Shown += (_, _) =>
            {
                if (pg >= 0 && pg + 1 < args.Length) mainWindow.ShowPageByName(args[pg + 1]);
                mainWindow.ShowDemoFrame();
            };
            int ms = Array.IndexOf(args, "--shot");
            if (ms >= 0 && ms + 1 < args.Length)
            {
                var shotTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                shotTimer.Tick += (_, _) =>
                {
                    shotTimer.Stop();
                    using var bmp = new Bitmap(mainWindow.Width, mainWindow.Height);
                    mainWindow.DrawToBitmap(bmp, new Rectangle(0, 0, mainWindow.Width, mainWindow.Height));
                    bmp.Save(args[ms + 1], System.Drawing.Imaging.ImageFormat.Png);
                    Application.Exit();
                };
                shotTimer.Start();
            }
            Application.Run(mainWindow);
            return 0;
        }

        // Opens only the theme editor, without touching the screen (for development).
        if (args.Contains("--editor-only"))
        {
            var s = AppSettings.Load();
            ThemeStore.EnsureDefault();
            using var eng = new PanelEngine(s);
            int tg = Array.IndexOf(args, "--theme-gallery");
            var startTheme = tg >= 0 && tg + 1 < args.Length && Enum.TryParse<WidgetCategory>(args[tg + 1], true, out var galleryCategory)
                ? DevTools.Gallery(galleryCategory)
                : ThemeStore.Load(Array.IndexOf(args, "--theme-name") is int tn && tn >= 0 && tn + 1 < args.Length ? args[tn + 1] : s.ThemeName);
            Log.Write($"editor-only: theme {startTheme.Name}, {startTheme.Elements.Count} layers, args: {string.Join(" ", args)}");
            var editor = new ThemeEditorForm(startTheme, eng, s, _ => { }) { TopMost = args.Contains("--shot") };
            int sel = Array.IndexOf(args, "--select");
            if (sel >= 0 && sel + 1 < args.Length) editor.Shown += (_, _) => editor.SelectByName(args[sel + 1]);
            int st = Array.IndexOf(args, "--selftest");
            if (st >= 0 && st + 1 < args.Length)
            {
                editor.Shown += (_, _) =>
                {
                    string report;
                    try { report = editor.SelfTest(); } catch (Exception ex) { report = "SELFTEST CRASHED: " + ex; }
                    File.WriteAllText(args[st + 1], report);
                    editor.Close();
                };
                editor.SuppressPrompts = true;
            }
            int cat = Array.IndexOf(args, "--category");
            if (cat >= 0 && cat + 1 < args.Length) editor.Shown += (_, _) => editor.ShowCategory(args[cat + 1]);
            int shot = Array.IndexOf(args, "--shot");
            if (shot >= 0 && shot + 1 < args.Length)
            {
                // Save a picture of the editor itself, then quit.
                var timer = new System.Windows.Forms.Timer { Interval = 2500 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    using var bmp = new Bitmap(editor.Width, editor.Height);
                    editor.DrawToBitmap(bmp, new Rectangle(0, 0, editor.Width, editor.Height));
                    bmp.Save(args[shot + 1], System.Drawing.Imaging.ImageFormat.Png);
                    editor.Close();
                };
                timer.Start();
            }
            Application.Run(editor);
            return 0;
        }

        // CPU temperature and power need admin rights, so relaunch elevated unless told not to.
        if (!args.Contains("--no-elevate") && !IsAdmin())
        {
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ProcessPath!, string.Join(" ", args))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                });
                return 0;
            }
            catch (Win32Exception)
            {
                // UAC declined: keep running with limited sensors.
            }
        }

        using var mutex = new Mutex(true, @"Local\HurricaneBeastPanel.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("Hurricane Beast Panel is already running (check the tray).",
                "Hurricane Beast Panel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        Application.Run(new MainForm(args.Contains("--minimized")));
        return 0;
    }

    static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    static int RenderTest(string path, string? themeFile)
    {
        var settings = AppSettings.Load();
        using var sensors = new SensorService();
        sensors.Poll();
        Thread.Sleep(1500); // load/throughput sensors need two samples
        var snap = sensors.Poll();
        ThemeStore.EnsureDefault();
        // The theme argument is a JSON file path or a saved theme name; --rotate-cw / --rotate-ccw turn the layout first.
        var theme = themeFile != null && themeFile.StartsWith("@") ? ExtraThemes.ByKey(themeFile[1..]) : themeFile != null && File.Exists(themeFile)
            ? Theme.FromJson(File.ReadAllText(themeFile))
            : ThemeStore.Load(themeFile ?? settings.ThemeName);
        var argv = Environment.GetCommandLineArgs();
        if (argv.Contains("--rotate-cw")) theme.RotateLayout(true);
        if (argv.Contains("--rotate-ccw")) theme.RotateLayout(false);

        ThemeRenderer.Render(theme, snap, settings, theme.Width, theme.Height).Dispose(); // starts video decoding
        Thread.Sleep(900);
        using var bmp = ThemeRenderer.Render(theme, snap, settings, theme.Width, theme.Height);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Thread.Sleep(450);
        using (var later = ThemeRenderer.Render(theme, snap, settings, theme.Width, theme.Height))
            later.Save(path + ".later.png", System.Drawing.Imaging.ImageFormat.Png);
        // Time a full frame (draw + rotate + JPEG) to see what frame rate this theme can sustain.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int quality = 90;
        const int timedFrames = 15;
        for (int i = 0; i < timedFrames; i++)
        {
            using var timed = ThemeRenderer.Render(theme, snap, settings, theme.Width, theme.Height);
            timed.RotateFlip(RotateFlipType.Rotate270FlipNone);
            PanelEngine.EncodeJpeg(timed, 90 * 1024, ref quality);
        }
        double msPerFrame = clock.Elapsed.TotalMilliseconds / timedFrames;
        int size = PanelEngine.EncodeJpeg(bmp, 90 * 1024).Length;
        File.WriteAllText(path + ".txt", $"ms/frame={msPerFrame:0.0}, jpeg {size} bytes, admin={IsAdmin()}, sensorError={sensors.OpenError}, " +
            $"cpuTemp={snap.CpuTemp}, gpuTemp={snap.GpuTemp}, cpuLoad={snap.CpuLoad}, ram={snap.RamLoad}");
        return 0;
    }
}
