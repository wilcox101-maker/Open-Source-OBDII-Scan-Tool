using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using ObdLink.Core.Dashboard;

namespace ObdLink.App;

/// <summary>Monitor working areas in device-independent pixels. Primary is first so a missing target can fall back to it.</summary>
public static class MonitorAreas
{
    public static IReadOnlyList<DashboardWindowPlacement.MonitorArea> WorkingAreasInDips(Visual visual)
    {
        var found = new List<(DashboardWindowPlacement.MonitorArea Area, bool Primary)>();
        MonitorEnumProc cb = (IntPtr h, IntPtr hdc, IntPtr rect, IntPtr data) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(h, ref info)) return true;
            uint dpiX = 96, dpiY = 96;
            try { GetDpiForMonitor(h, 0, out dpiX, out dpiY); }
            catch (Exception) { dpiX = dpiY = 96; }
            if (dpiX == 0) dpiX = 96;
            if (dpiY == 0) dpiY = 96;
            double sx = 96.0 / dpiX, sy = 96.0 / dpiY;
            var work = info.rcWork;
            found.Add((new DashboardWindowPlacement.MonitorArea(
                work.Left * sx, work.Top * sy, (work.Right - work.Left) * sx, (work.Bottom - work.Top) * sy),
                (info.dwFlags & 1) != 0));
            return true;
        };
        try { EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero); }
        catch (Exception) { found.Clear(); }
        GC.KeepAlive(cb);
        if (found.Count == 0)
        {
            var w = SystemParameters.WorkArea;
            return new[] { new DashboardWindowPlacement.MonitorArea(w.Left, w.Top, w.Width, w.Height) };
        }
        return found.OrderByDescending(x => x.Primary).Select(x => x.Area).ToList();
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
