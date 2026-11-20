using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace HurricaneBeastPanel;

/// <summary>What the FPS counter currently sees.</summary>
public sealed record FpsReading(float Fps, float Low1Fps, float FrameTimeMs, string Game);

/// <summary>
/// Game frame rate without any extra program: listens to Windows' own "a frame was presented" events (ETW,
/// provider Microsoft-Windows-DXGI, the same source PresentMon uses) and counts them per process.
/// Works for DirectX 9/10/11/12 games and needs administrator rights, which the app asks for at start.
/// The game shown is the foreground program if it is presenting frames, otherwise the busiest one.
/// </summary>
public sealed class FpsService : IDisposable
{
    const string SessionName = "HurricaneBeastPanelFps";
    static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    const int PresentStart = 42;
    const double WindowMs = 10_000;      // history kept for the 1% low
    const int MinFps = 5;              // slower than this is not a game, it is a window repainting

    // Programs that present all the time but are never the game.
    static readonly HashSet<string> Ignore = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm", "explorer", "HurricaneBeastPanel", "SearchHost", "ShellExperienceHost", "StartMenuExperienceHost", "TextInputHost",
        "LockApp", "RuntimeBroker", "ApplicationFrameHost", "SystemSettings", "msedgewebview2", "Taskmgr",
        "claude", "chrome", "msedge", "firefox", "brave", "opera", "discord", "Code", "slack", "Teams", "ms-teams", "spotify",
        "WindowsTerminal", "powershell", "pwsh", "cmd", "conhost", "notepad", "devenv", "GitHubDesktop", "steamwebhelper", "EpicWebHelper",
    };

    readonly object _lock = new();
    readonly Dictionary<int, Queue<double>> _frames = new();   // process id -> frame times (Stopwatch ms)
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly int _self = Environment.ProcessId;
    TraceEventSession? _session;
    Thread? _thread;
    FpsReading? _last;
    long _lastTick;
    readonly Dictionary<int, string> _names = new();

    public string? Error { get; private set; }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    public FpsService()
    {
        try
        {
            if (!(TraceEventSession.IsElevated() ?? false)) { Error = "FPS needs administrator rights"; return; }
            // A session left behind by a crashed run would block ours.
            try { TraceEventSession.GetActiveSession(SessionName)?.Stop(); } catch { }
            _session = new TraceEventSession(SessionName) { StopOnDispose = true };
            _session.EnableProvider(DxgiProvider, TraceEventLevel.Informational);
            _thread = new Thread(Pump) { IsBackground = true, Name = "FPS events" };
            _thread.Start();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Log.Write("FPS counter unavailable: " + ex.Message);
        }
    }

    void Pump()
    {
        try
        {
            _session!.Source.Dynamic.All += OnEvent;
            _session.Source.Process();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    void OnEvent(TraceEvent e)
    {
        if ((int)e.ID != PresentStart || e.ProviderGuid != DxgiProvider) return;
        int pid = e.ProcessID;
        if (pid == _self || pid <= 4) return;
        double now = _clock.Elapsed.TotalMilliseconds;
        lock (_lock)
        {
            if (!_frames.TryGetValue(pid, out var q)) _frames[pid] = q = new Queue<double>();
            q.Enqueue(now);
            while (q.Count > 0 && now - q.Peek() > WindowMs) q.Dequeue();
        }
    }

    string NameOf(int pid)
    {
        if (_names.TryGetValue(pid, out var n)) return n;
        try { n = Process.GetProcessById(pid).ProcessName; } catch { n = ""; }
        _names[pid] = n;
        return n;
    }

    static int ForegroundPid()
    {
        var h = GetForegroundWindow();
        return h != IntPtr.Zero && GetWindowThreadProcessId(h, out uint pid) != 0 ? (int)pid : 0;
    }

    /// <summary>Current reading, or null when no game is presenting frames. Cheap; recalculated at most every 250 ms.</summary>
    public FpsReading? Read()
    {
        if (_session == null) return null;
        long tick = _clock.ElapsedMilliseconds;
        if (tick - _lastTick < 250) return _last;
        _lastTick = tick;
        double now = _clock.Elapsed.TotalMilliseconds;

        double[] frames;
        int pid;
        lock (_lock)
        {
            foreach (int dead in _frames.Where(kv => kv.Value.Count == 0 || now - kv.Value.Last() > 3000).Select(kv => kv.Key).ToList())
                _frames.Remove(dead);

            int RecentCount(int p) => _frames.TryGetValue(p, out var q) ? q.Count(t => now - t <= 1000) : 0;

            pid = ForegroundPid();
            if (RecentCount(pid) < MinFps || Ignore.Contains(NameOf(pid)))
            {
                pid = _frames.Keys.Where(p => !Ignore.Contains(NameOf(p)))
                    .OrderByDescending(RecentCount).FirstOrDefault();
                if (RecentCount(pid) < MinFps) { _last = null; return null; }
            }
            frames = _frames[pid].ToArray();
        }

        // Frames per second over the last second, with the fraction of a second the first and last frame cover.
        var lastSecond = frames.Where(t => now - t <= 1000).ToArray();
        float span = lastSecond.Length > 1 ? (float)(lastSecond[^1] - lastSecond[0]) : 0f;
        float fps = span >= 1f ? Math.Min(1000f, (lastSecond.Length - 1) * 1000f / span) : lastSecond.Length;

        // Frame times over the whole window; the 1% low is the frame rate of the slowest 1% of frames.
        var times = new List<float>(frames.Length);
        for (int i = 1; i < frames.Length; i++) times.Add((float)(frames[i] - frames[i - 1]));
        float low1 = fps;
        float frameTime = fps > 0 ? 1000f / fps : 0f;
        if (times.Count >= 20)
        {
            times.Sort();
            int from = Math.Min(times.Count - 1, (int)(times.Count * 0.99));
            float worst = times.Skip(from).Average();
            if (worst > 0) low1 = Math.Min(fps, 1000f / worst);
        }
        return _last = new FpsReading(fps, low1, frameTime, NameOf(pid));
    }

    public void Dispose()
    {
        try { _session?.Dispose(); } catch { }
        _session = null;
    }
}
