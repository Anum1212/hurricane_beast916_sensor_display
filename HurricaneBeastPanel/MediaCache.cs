using System.Diagnostics;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace HurricaneBeastPanel;

/// <summary>A decoded image, GIF or video that can be drawn frame by frame.</summary>
public abstract class MediaSource : IDisposable
{
    public abstract bool Animated { get; }

    /// <summary>Calls <paramref name="draw"/> with the current frame while holding the source's lock. False if there is no frame yet.</summary>
    public abstract bool WithFrame(Action<Bitmap> draw);

    public abstract void Dispose();
}

/// <summary>Still image or animated GIF, fully decoded into memory.</summary>
sealed class ImageSource : MediaSource
{
    readonly List<Bitmap> _frames = new();
    readonly List<int> _delays = new();
    readonly int _total;
    readonly object _lock = new();

    public override bool Animated => _frames.Count > 1;

    public ImageSource(string path)
    {
        using var ms = new MemoryStream(File.ReadAllBytes(path));
        using var img = Image.FromStream(ms);
        int count = 1;
        try { count = Math.Max(1, img.GetFrameCount(FrameDimension.Time)); } catch { }

        // Keep animated GIFs within a sane amount of memory by scaling frames down.
        float scale = 1f;
        if (count > 1)
        {
            double bytes = (double)count * img.Width * img.Height * 4;
            if (bytes > 256e6) scale = (float)Math.Sqrt(256e6 / bytes);
        }
        int w = Math.Max(1, (int)(img.Width * scale)), h = Math.Max(1, (int)(img.Height * scale));

        int[] delays = ReadDelays(img, count);
        for (int i = 0; i < count; i++)
        {
            if (count > 1) img.SelectActiveFrame(FrameDimension.Time, i);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, w, h);
            }
            _frames.Add(bmp);
            _delays.Add(delays[i]);
            _total += delays[i];
        }
    }

    static int[] ReadDelays(Image img, int count)
    {
        var delays = Enumerable.Repeat(100, count).ToArray();
        try
        {
            var item = img.GetPropertyItem(0x5100); // PropertyTagFrameDelay, in 1/100 s
            if (item?.Value != null)
            {
                for (int i = 0; i < count && (i + 1) * 4 <= item.Value.Length; i++)
                {
                    int ms = BitConverter.ToInt32(item.Value, i * 4) * 10;
                    delays[i] = ms <= 10 ? 100 : ms; // browsers treat tiny delays as 100 ms
                }
            }
        }
        catch { /* no delay info: use the default */ }
        return delays;
    }

    public override bool WithFrame(Action<Bitmap> draw)
    {
        lock (_lock)
        {
            if (_frames.Count == 0) return false;
            int idx = 0;
            if (_frames.Count > 1 && _total > 0)
            {
                long t = Environment.TickCount64 % _total;
                while (idx < _frames.Count - 1 && t >= _delays[idx]) { t -= _delays[idx]; idx++; }
            }
            draw(_frames[idx]);
            return true;
        }
    }

    public override void Dispose()
    {
        lock (_lock)
        {
            foreach (var f in _frames) f.Dispose();
            _frames.Clear();
        }
    }
}

/// <summary>MP4 and other video files, decoded on a background thread and looped. Audio is ignored.</summary>
sealed class VideoSource : MediaSource
{
    const int MaxWidth = 1280;

    readonly object _lock = new();
    readonly Thread _thread;
    Bitmap? _frame;
    volatile bool _stop;

    public override bool Animated => true;

    public VideoSource(string path)
    {
        // Fail early (on the caller's thread) if the file cannot be opened.
        using (var probe = new VideoCapture(path))
        {
            if (!probe.IsOpened()) throw new IOException("Cannot open video: " + path);
        }
        _thread = new Thread(() => Decode(path)) { IsBackground = true, Name = "Video decode" };
        _thread.Start();
    }

    void Decode(string path)
    {
        try
        {
            using var cap = new VideoCapture(path);
            double fps = cap.Fps;
            if (fps < 1 || fps > 120) fps = 30;
            double frameMs = 1000.0 / fps;
            var sw = new Stopwatch();
            using var mat = new Mat();
            while (!_stop)
            {
                sw.Restart();
                if (!cap.Read(mat) || mat.Empty())
                {
                    cap.Set(VideoCaptureProperties.PosFrames, 0); // loop
                    continue;
                }

                Mat src = mat;
                Mat? small = null;
                if (mat.Width > MaxWidth)
                {
                    small = new Mat();
                    Cv2.Resize(mat, small, new OpenCvSharp.Size(MaxWidth, (int)(mat.Height * (double)MaxWidth / mat.Width)));
                    src = small;
                }
                var bmp = BitmapConverter.ToBitmap(src);
                small?.Dispose();

                lock (_lock)
                {
                    _frame?.Dispose();
                    _frame = bmp;
                }

                int wait = (int)(frameMs - sw.ElapsedMilliseconds);
                if (wait > 0) Thread.Sleep(wait);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Video decode stopped: " + ex.Message);
        }
    }

    public override bool WithFrame(Action<Bitmap> draw)
    {
        lock (_lock)
        {
            if (_frame == null) return false;
            draw(_frame);
            return true;
        }
    }

    public override void Dispose()
    {
        _stop = true;
        _thread.Join(1500);
        lock (_lock)
        {
            _frame?.Dispose();
            _frame = null;
        }
    }
}

public static class MediaCache
{
    static readonly object Lock = new();
    static readonly Dictionary<string, MediaSource?> Sources = new(StringComparer.OrdinalIgnoreCase);
    static readonly string[] VideoExtensions = { ".mp4", ".m4v", ".mov", ".avi", ".mkv", ".webm", ".wmv" };

    public static string MediaDir => Path.Combine(AppSettings.Dir, "media");

    public static bool IsVideo(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Turns a stored path (relative to the app data folder, or absolute) into a full path.</summary>
    public static string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppSettings.Dir, path);

    /// <summary>Copies a file into the app's media folder so themes keep working if the original moves. Returns the stored (relative) path.</summary>
    public static string Import(string sourcePath)
    {
        Directory.CreateDirectory(MediaDir);
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string ext = Path.GetExtension(sourcePath);
        string dest = Path.Combine(MediaDir, name + ext);
        for (int i = 2; File.Exists(dest); i++)
        {
            if (new FileInfo(dest).Length == new FileInfo(sourcePath).Length) break; // same file already imported
            dest = Path.Combine(MediaDir, $"{name} ({i}){ext}");
        }
        if (!File.Exists(dest)) File.Copy(sourcePath, dest);
        return Path.GetRelativePath(AppSettings.Dir, dest);
    }

    public static MediaSource? Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        lock (Lock)
        {
            if (Sources.TryGetValue(path, out var cached)) return cached;
            MediaSource? source = null;
            try
            {
                string full = Resolve(path);
                if (File.Exists(full))
                    source = IsVideo(full) ? new VideoSource(full) : new ImageSource(full);
                else
                    Log.Write("Media file not found: " + full);
            }
            catch (Exception ex) { Log.Write($"Media load failed ({path}): {ex.Message}"); }
            Sources[path] = source; // failures are cached too so they are not retried every frame
            return source;
        }
    }

    /// <summary>Disposes every source that is not in <paramref name="inUse"/>.</summary>
    public static void Prune(IEnumerable<string> inUse)
    {
        var keep = new HashSet<string>(inUse, StringComparer.OrdinalIgnoreCase);
        lock (Lock)
        {
            foreach (var key in Sources.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                Sources[key]?.Dispose();
                Sources.Remove(key);
            }
        }
    }

    /// <summary>Forgets a path so it is decoded again next time (after the file changed).</summary>
    public static void Forget(string path) => Prune(Sources.Keys.Where(k => !string.Equals(k, path, StringComparison.OrdinalIgnoreCase)).ToList());
}
