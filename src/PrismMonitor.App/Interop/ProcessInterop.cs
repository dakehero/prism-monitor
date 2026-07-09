using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PrismMonitor.App.Interop;

internal static partial class ProcessInterop
{
    internal const int ErrorInsufficientBuffer = 122;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process2(
        SafeProcessHandle process,
        out ushort processMachine,
        out ushort nativeMachine);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryFullProcessImageName(
        SafeProcessHandle process,
        uint flags,
        Span<char> executableName,
        ref uint size);

    [LibraryImport("kernel32.dll", SetLastError = false, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetPackageFullName(
        SafeProcessHandle process,
        ref uint packageFullNameLength,
        Span<char> packageFullName);
}
