using System.Reflection.PortableExecutable;

namespace NativeGuard.Core.Processes;

public sealed record ProcessArchitectureInfo(bool IsNonNative, string DisplayName);

public static class ProcessArchitectureClassifier
{
    public const ushort Arm64EcMachine = 0xa641;

    public static ProcessArchitectureInfo Classify(ushort processMachine, ushort nativeMachine)
    {
        string displayName = GetDisplayName(processMachine);
        bool isArm64Host = nativeMachine == (ushort)Machine.Arm64;
        bool isNonNative = isArm64Host
            && (processMachine == (ushort)Machine.Amd64
                || processMachine == (ushort)Machine.I386
                || processMachine == Arm64EcMachine);

        return new ProcessArchitectureInfo(isNonNative, displayName);
    }

    private static string GetDisplayName(ushort machineType)
    {
        return machineType switch
        {
            (ushort)Machine.I386 => "x86",
            (ushort)Machine.Amd64 => "x64",
            (ushort)Machine.Arm64 => "ARM64",
            Arm64EcMachine => "ARM64EC",
            _ => "Unknown"
        };
    }
}
