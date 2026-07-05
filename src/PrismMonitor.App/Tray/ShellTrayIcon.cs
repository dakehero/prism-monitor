using System.Runtime.InteropServices;
using System.Diagnostics;
using PrismMonitor.Core.Ui;
using PrismMonitor.App.Interop;

namespace PrismMonitor.App.Tray;

internal sealed class ShellTrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const uint ExitMenuCommand = 100;
    private const uint CallbackMessage = ShellNotifyIconInterop.WindowMessageApp + 1;
    private readonly ShellNotifyIconInterop.WindowProc _windowProc;
    private readonly IntPtr _messageWindow;
    private readonly IntPtr _baseIcon;
    private IntPtr _currentIcon;
    private readonly Action _openRequested;
    private readonly Action _exitRequested;
    private readonly Func<Task<TrayStatus>> _statusProvider;
    private TrayStatus _cachedStatus = new("Prism Monitor", [], 0);
    private bool _disposed;
    private int _displayedProcessCount = -1;

    public ShellTrayIcon(Action openRequested, Action exitRequested, Func<Task<TrayStatus>> statusProvider)
    {
        _openRequested = openRequested;
        _exitRequested = exitRequested;
        _statusProvider = statusProvider;
        _windowProc = WndProc;

        const string className = "PrismMonitorTrayWindow";
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
        _baseIcon = ShellNotifyIconInterop.LoadImage(
            IntPtr.Zero,
            iconPath,
            ShellNotifyIconInterop.ImageIcon,
            0,
            0,
            ShellNotifyIconInterop.LoadFromFile | ShellNotifyIconInterop.LoadDefaultSize);
        _currentIcon = _baseIcon;

        AddOrUpdate("Prism Monitor");
    }

    public async Task RefreshTooltipAsync()
    {
        try
        {
            TrayStatus status = await _statusProvider().ConfigureAwait(false);
            UpdateStatus(status);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public void UpdateStatus(TrayStatus status)
    {
        if (_disposed)
        {
            return;
        }

        _cachedStatus = status;
        UpdateIcon(status.ProcessCount);
        AddOrUpdate(status.Tooltip);
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

        NotifyIconData data = CreateData("Prism Monitor");
        _ = ShellNotifyIconInterop.ShellNotifyIcon(ShellNotifyIconInterop.NotifyIconDelete, ref data);

        if (_currentIcon != IntPtr.Zero && _currentIcon != _baseIcon)
        {
            _ = ShellNotifyIconInterop.DestroyIcon(_currentIcon);
        }

        if (_baseIcon != IntPtr.Zero)
        {
            _ = ShellNotifyIconInterop.DestroyIcon(_baseIcon);
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
                    AddOrUpdate(_cachedStatus.Tooltip);
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
            TrayStatus status = _cachedStatus;
            if (status.TopProcesses.Count > 0)
            {
                foreach (string process in status.TopProcesses)
                {
                    _ = ShellNotifyIconInterop.AppendMenu(
                        menu,
                        ShellNotifyIconInterop.MenuString | ShellNotifyIconInterop.MenuDisabled,
                        UIntPtr.Zero,
                        process);
                }

                _ = ShellNotifyIconInterop.AppendMenu(menu, ShellNotifyIconInterop.MenuSeparator, UIntPtr.Zero, string.Empty);
            }

            _ = ShellNotifyIconInterop.AppendMenu(
                menu,
                ShellNotifyIconInterop.MenuString,
                new UIntPtr(ExitMenuCommand),
                "Exit");

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
            Icon = _currentIcon,
            Tip = TruncateTooltip(tooltip)
        };
    }

    private void UpdateIcon(int processCount)
    {
        if (_displayedProcessCount == processCount)
        {
            return;
        }

        IntPtr previousIcon = _currentIcon;
        _currentIcon = TrayIconBadgeFactory.CreateBadgedIcon(_baseIcon, processCount);
        _displayedProcessCount = processCount;

        if (previousIcon != IntPtr.Zero && previousIcon != _baseIcon)
        {
            _ = ShellNotifyIconInterop.DestroyIcon(previousIcon);
        }
    }

    private static string TruncateTooltip(string tooltip)
    {
        return tooltip.Length < 128 ? tooltip : tooltip[..127];
    }
}
