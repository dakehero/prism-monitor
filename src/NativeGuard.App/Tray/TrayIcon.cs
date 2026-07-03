using System.Runtime.InteropServices;
using NativeGuard_App.Interop;

namespace NativeGuard_App.Tray;

internal sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = NativeMethods.WindowMessageApp + 1;
    private readonly NativeMethods.WindowProc _windowProc;
    private readonly IntPtr _messageWindow;
    private readonly IntPtr _icon;
    private readonly Action _openRequested;
    private readonly Action _exitRequested;
    private readonly Func<Task<string>> _tooltipProvider;
    private bool _disposed;

    public TrayIcon(Action openRequested, Action exitRequested, Func<Task<string>> tooltipProvider)
    {
        _openRequested = openRequested;
        _exitRequested = exitRequested;
        _tooltipProvider = tooltipProvider;
        _windowProc = WndProc;

        const string className = "NativeGuardTrayWindow";
        WindowClass windowClass = new()
        {
            WindowProc = _windowProc,
            ClassName = className
        };

        _ = NativeMethods.RegisterClass(ref windowClass);
        _messageWindow = NativeMethods.CreateWindowEx(
            0,
            className,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _icon = NativeMethods.LoadImage(
            IntPtr.Zero,
            iconPath,
            NativeMethods.ImageIcon,
            0,
            0,
            NativeMethods.LoadFromFile | NativeMethods.LoadDefaultSize);

        AddOrUpdate("Native Guard");
    }

    public async Task RefreshTooltipAsync()
    {
        string tooltip = await _tooltipProvider().ConfigureAwait(false);
        AddOrUpdate(tooltip);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NotifyIconData data = CreateData("Native Guard");
        _ = NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconDelete, ref data);

        if (_icon != IntPtr.Zero)
        {
            _ = NativeMethods.DestroyIcon(_icon);
        }

        if (_messageWindow != IntPtr.Zero)
        {
            _ = NativeMethods.DestroyWindow(_messageWindow);
        }

        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == CallbackMessage)
        {
            uint trayMessage = unchecked((uint)lParam.ToInt64());
            if (trayMessage == NativeMethods.WindowMessageLeftButtonUp)
            {
                _openRequested();
            }
            else if (trayMessage == NativeMethods.WindowMessageRightButtonUp)
            {
                _exitRequested();
            }
            else if (trayMessage == NativeMethods.WindowMessageMouseMove)
            {
                _ = RefreshTooltipAsync();
            }
        }

        return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void AddOrUpdate(string tooltip)
    {
        if (_disposed || _messageWindow == IntPtr.Zero)
        {
            return;
        }

        NotifyIconData data = CreateData(tooltip);
        _ = NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconModify, ref data)
            || NativeMethods.ShellNotifyIcon(NativeMethods.NotifyIconAdd, ref data);
    }

    private NotifyIconData CreateData(string tooltip)
    {
        return new NotifyIconData
        {
            Size = checked((uint)Marshal.SizeOf<NotifyIconData>()),
            WindowHandle = _messageWindow,
            Id = IconId,
            Flags = NativeMethods.NotifyIconMessage | NativeMethods.NotifyIconIcon | NativeMethods.NotifyIconTip,
            CallbackMessage = CallbackMessage,
            Icon = _icon,
            Tip = TruncateTooltip(tooltip)
        };
    }

    private static string TruncateTooltip(string tooltip)
    {
        return tooltip.Length < 128 ? tooltip : tooltip[..127];
    }
}
