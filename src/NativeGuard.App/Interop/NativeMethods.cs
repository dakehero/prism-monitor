using System.Runtime.InteropServices;

namespace NativeGuard_App.Interop;

internal static partial class NativeMethods
{
    internal const uint ProcessQueryLimitedInformation = 0x1000;

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumProcesses(
        [Out] uint[] processIds,
        uint bytes,
        out uint bytesReturned);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial IntPtr OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process2(
        IntPtr process,
        out ushort processMachine,
        out ushort nativeMachine);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetProcessTimes(
        IntPtr process,
        out FileTime creationTime,
        out FileTime exitTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryFullProcessImageName(
        IntPtr process,
        uint flags,
        Span<char> executableName,
        ref uint size);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct FileTime
{
    private readonly uint _lowDateTime;
    private readonly uint _highDateTime;

    public long ToTicks()
    {
        return ((long)_highDateTime << 32) | _lowDateTime;
    }
}
