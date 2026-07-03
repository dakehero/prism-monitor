using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NativeGuard_App.Interop;

internal static partial class NativeMethods
{
    internal delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    internal const int ImageIcon = 1;
    internal const uint LoadFromFile = 0x00000010;
    internal const uint LoadDefaultSize = 0x00000040;
    internal const uint NotifyIconAdd = 0x00000000;
    internal const uint NotifyIconModify = 0x00000001;
    internal const uint NotifyIconDelete = 0x00000002;
    internal const uint NotifyIconMessage = 0x00000001;
    internal const uint NotifyIconIcon = 0x00000002;
    internal const uint NotifyIconTip = 0x00000004;
    internal const uint WindowMessageApp = 0x8000;
    internal const uint WindowMessageMouseMove = 0x0200;
    internal const uint WindowMessageLeftButtonUp = 0x0202;
    internal const uint WindowMessageRightButtonUp = 0x0205;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process2(
        SafeProcessHandle process,
        out ushort processMachine,
        out ushort nativeMachine);

    [DllImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClass(ref WindowClass windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parentWindow,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadImage(
        IntPtr instance,
        string name,
        uint type,
        int desiredWidth,
        int desiredHeight,
        uint loadFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr icon);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WindowClass
{
    public uint Style;
    public NativeMethods.WindowProc WindowProc;
    public int ClassExtraBytes;
    public int WindowExtraBytes;
    public IntPtr Instance;
    public IntPtr Icon;
    public IntPtr Cursor;
    public IntPtr Background;
    public string? MenuName;
    public string ClassName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NotifyIconData
{
    public uint Size;
    public IntPtr WindowHandle;
    public uint Id;
    public uint Flags;
    public uint CallbackMessage;
    public IntPtr Icon;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string Tip;
}
