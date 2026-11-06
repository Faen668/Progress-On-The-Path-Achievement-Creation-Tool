using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AchievementCreationTool.Controls;

/// <summary>Keeps a window on the screen it opens on: no bigger than that screen's work area (the taskbar left out),
/// and moved in so none of it is off an edge. Each monitor's own size counts, whatever the others are.</summary>
public static class OnScreen
{
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    const uint NearestMonitor = 2;

    /// <summary>Call once, before the window's shown: it fits it as soon as it has a handle and a size</summary>
    public static void Keep(Window window)
    {
        window.SourceInitialized += (_, _) => Fit(window);
        window.Loaded += (_, _) => Fit(window);
    }

    /// <summary>The work area of the monitor a window is on, in the window's own units</summary>
    public static Rect WorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return SystemParameters.WorkArea;
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, NearestMonitor), ref info))
            return SystemParameters.WorkArea;
        var toUnits = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = toUnits.Transform(new Point(info.rcWork.Left, info.rcWork.Top));
        var bottomRight = toUnits.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    public static void Fit(Window window)
    {
        if (window.WindowState != WindowState.Normal || window.Left < -10000)   // maximised, or off screen on purpose (the pictures)
            return;
        var work = WorkArea(window);
        var width = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        var height = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
        if (width > work.Width)
            window.Width = width = work.Width;
        if (height > work.Height)
            window.Height = height = work.Height;
        if (double.IsNaN(window.Left) || double.IsNaN(window.Top))
            return;
        window.Left = Math.Max(work.Left, Math.Min(window.Left, work.Right - width));
        window.Top = Math.Max(work.Top, Math.Min(window.Top, work.Bottom - height));
    }
}
