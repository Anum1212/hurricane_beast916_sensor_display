using System.Runtime.InteropServices;

namespace HurricaneBeastPanel;

/// <summary>
/// Reads the Windows master volume and mute state of the default playback device (Core Audio, no extra packages).
/// The endpoint is looked up again every few seconds so a changed default device is followed.
/// </summary>
static class AudioVolume
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    static readonly object Lock = new();
    static IAudioEndpointVolume? _endpoint;
    static long _endpointTick;
    static long _failTick;

    /// <summary>Fills in the volume (0-100) and mute state; leaves them null when there is no playback device.</summary>
    public static void Read(SensorSnapshot s)
    {
        lock (Lock)
        {
            try
            {
                long now = Environment.TickCount64;
                if (_endpoint == null && now - _failTick < 5000) return; // no device: do not retry every second
                if (_endpoint == null || now - _endpointTick > 5000)
                {
                    Release();
                    _endpoint = Open();
                    _endpointTick = now;
                }
                if (_endpoint == null) { _failTick = now; return; }
                if (_endpoint.GetMasterVolumeLevelScalar(out float level) != 0) { Release(); return; }
                _endpoint.GetMute(out bool muted);
                s.VolumePercent = MathF.Round(Math.Clamp(level, 0f, 1f) * 100f);
                s.VolumeMuted = muted;
            }
            catch
            {
                Release();
                _failTick = Environment.TickCount64;
            }
        }
    }

    static IAudioEndpointVolume? Open()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        try
        {
            // 0 = render (speakers/headphones), 1 = multimedia role
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var device) != 0 || device == null) return null;
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                if (device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var obj) != 0) return null;
                return obj as IAudioEndpointVolume;
            }
            finally { Marshal.ReleaseComObject(device); }
        }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    static void Release()
    {
        if (_endpoint != null)
        {
            try { Marshal.ReleaseComObject(_endpoint); } catch { }
            _endpoint = null;
        }
    }
}
