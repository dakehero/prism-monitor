using System.Runtime.InteropServices;
using System.Diagnostics;
using NativeGuard.Core.Ui;
using NativeGuard_App.Interop;

namespace NativeGuard_App.Tray;

internal sealed class ShellTrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const uint ExitMenuCommand = 100;
    private const uint CallbackMessage = ShellNotifyIconInterop.WindowMessageApp + 1;
    private readonly ShellNotifyIconInterop.WindowProc _windowProc;
    private readonly IntPtr _messageWindow;
    private readonly IntPtr _icon;
    private readonly Action _openRequested;
    private readonly Action _exitRequested;
    private readonly Func<Task<string>> _tooltipProvider;
    private bool _disposed;

    public ShellTrayIcon(Action openRequested, Action exitRequested, Func<Task<string>> tooltipProvider)
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

        _ = ShellNotifyIconInterop.RegisterClass(ref windowClass);
        _messageWindow = ShellNotifyIconInterop.CreateWindowEx(
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
        _icon = ShellNotifyIconInterop.LoadImage(
            IntPtr.Zero,
            iconPath,
            ShellNotifyIconInterop.ImageIcon,
            0,
            0,
            ShellNotifyIconInterop.LoadFromFile | ShellNotifyIconInterop.LoadDefaultSize);

        AddOrUpdate("Native Guard");
    }

    public async Task RefreshTooltipAsync()
    {
        try
        {
            string tooltip = await _tooltipProvider().ConfigureAwait(false);
            AddOrUpdate(tooltip);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public bool TryGetIconRect(out ScreenRect rect)
    {
        NotifyIconIdentifier identifier = new()
        {
            Size = checked((uint)Marshal.SizeOf<NotifyIconIdentifier>()),
            WindowHandle = _messageWindow,
            Id = IconId
        };

        int result = ShellNotifyIconInterop.Shell_NotifyIconGetRect(ref identifier, out NativeRect nativeRect);
        if (result != 0)
        {
            rect = default;
            return false;
        }

        rect = new ScreenRect(
            nativeRect.Left,
            nativeRect.Top,
            nativeRect.Right - nativeRect.Left,
            nativeRect.Bottom - nativeRect.Top);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NotifyIconData data = CreateData("Native Guard");
        _ = ShellNotifyIconInterop.ShellNotifyIcon(ShellNotifyIconInterop.NotifyIconDelete, ref data);

        if (_icon != IntPtr.Zero)
        {
            _ = ShellNotifyIconInterop.DestroyIcon(_icon);
        }

        if (_messageWindow != IntPtr.Zero)
        {
            _ = ShellNotifyIconInterop.DestroyWindow(_messageWindow);
        }

        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == CallbackMessage)
        {
            try
            {
                uint trayMessage = unchecked((uint)lParam.ToInt64());
                if (trayMessage == ShellNotifyIconInterop.WindowMessageLeftButtonUp)
                {
                    _openRequested();
                }
                else if (trayMessage == ShellNotifyIconInterop.WindowMessageRightButtonUp)
                {
                    ShowContextMenu();
                }
                else if (trayMessage == ShellNotifyIconInterop.WindowMessageMouseMove)
                {
                    _ = RefreshTooltipAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        return ShellNotifyIconInterop.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_disposed || _messageWindow == IntPtr.Zero)
        {
            return;
        }

        IntPtr menu = ShellNotifyIconInterop.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _ = ShellNotifyIconInterop.AppendMenu(
                menu,
                ShellNotifyIconInterop.MenuString,
                new UIntPtr(ExitMenuCommand),
                "退出");

            if (!ShellNotifyIconInterop.GetCursorPos(out NativePoint cursor))
            {
                return;
            }

            _ = ShellNotifyIconInterop.SetForegroundWindow(_messageWindow);
            uint command = ShellNotifyIconInterop.TrackPopupMenu(
                menu,
                ShellNotifyIconInterop.TrackPopupMenuReturnCommand
                    | ShellNotifyIconInterop.TrackPopupMenuRightButton
                    | ShellNotifyIconInterop.TrackPopupMenuLeftAlign
                    | ShellNotifyIconInterop.TrackPopupMenuBottomAlign,
                cursor.X,
                cursor.Y,
                0,
                _messageWindow,
                IntPtr.Zero);

            _ = ShellNotifyIconInterop.PostMessage(
                _messageWindow,
                ShellNotifyIconInterop.WindowMessageNull,
                IntPtr.Zero,
                IntPtr.Zero);

            if (command == ExitMenuCommand)
            {
                _exitRequested();
            }
        }
        finally
        {
            _ = ShellNotifyIconInterop.DestroyMenu(menu);
        }
    }

    private void AddOrUpdate(string tooltip)
    {
        if (_disposed || _messageWindow == IntPtr.Zero)
        {
            return;
        }

        NotifyIconData data = CreateData(tooltip);
        _ = ShellNotifyIconInterop.ShellNotifyIcon(ShellNotifyIconInterop.NotifyIconModify, ref data)
            || ShellNotifyIconInterop.ShellNotifyIcon(ShellNotifyIconInterop.NotifyIconAdd, ref data);
    }

    private NotifyIconData CreateData(string tooltip)
    {
        return new NotifyIconData
        {
            Size = checked((uint)Marshal.SizeOf<NotifyIconData>()),
            WindowHandle = _messageWindow,
            Id = IconId,
            Flags = ShellNotifyIconInterop.NotifyIconMessage | ShellNotifyIconInterop.NotifyIconIcon | ShellNotifyIconInterop.NotifyIconTip,
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
