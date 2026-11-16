using System.Runtime.InteropServices;
using System.Windows;
using ObdLink.Core.Flash;

namespace ObdLink.App;

/// <summary>
/// AC power state (GetSystemPowerStatus) and sleep blocking (SetThreadExecutionState) for flash jobs. The execution state belongs to
/// the thread that sets it, so both calls run on the UI thread, which lives as long as the app.
/// </summary>
public sealed class WindowsHostPower : IHostPower
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemPowerStatus(out SystemPowerStatus s);
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
    private const uint EsContinuous = 0x80000000, EsSystemRequired = 0x00000001;

    public bool? OnAcPower
    {
        get
        {
            try { return GetSystemPowerStatus(out var s) ? s.ACLineStatus switch { 1 => true, 0 => false, _ => null } : null; }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return null; } // not Windows
        }
    }

    public IDisposable KeepAwake()
    {
        OnUi(() => SetThreadExecutionState(EsContinuous | EsSystemRequired));
        return new Release();
    }

    private sealed class Release : IDisposable
    {
        private int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) OnUi(() => SetThreadExecutionState(EsContinuous)); }
    }

    private static void OnUi(Action a)
    {
        try
        {
            var d = Application.Current?.Dispatcher;
            if (d is null || d.CheckAccess()) a(); else d.Invoke(a);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { Serilog.Log.Warning("Sleep blocking unavailable: {Err}", ex.Message); }
    }
}
