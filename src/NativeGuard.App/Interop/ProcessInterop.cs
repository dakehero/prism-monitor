using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NativeGuard_App.Interop;

internal static partial class ProcessInterop
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process2(
        SafeProcessHandle process,
        out ushort processMachine,
        out ushort nativeMachine);
}
