using System.Reflection.PortableExecutable;

namespace NativeGuard.Core.Processes;

public sealed record ProcessArchitectureInfo(bool IsNonNative, string DisplayName);

public static class ProcessArchitectureClassifier
{
    public const ushort Arm64EcMachine = 0xa641;

    public static ProcessArchitectureInfo Classify(ushort processMachine, ushort nativeMachine)
    {
        return Classify(processMachine, nativeMachine, imageMachine: null);
    }

    public static ProcessArchitectureInfo Classify(ushort processMachine, ushort nativeMachine, ushort? imageMachine)
    {
        string displayName = GetDisplayName(processMachine);
        ushort effectiveMachine = processMachine == (ushort)Machine.Unknown && imageMachine.HasValue
            ? imageMachine.Value
            : processMachine;

        if (processMachine == (ushort)Machine.Unknown && imageMachine.HasValue)
        {
            displayName = GetDisplayName(imageMachine.Value);
        }

        bool isArm64Host = nativeMachine == (ushort)Machine.Arm64;
        bool isNonNative = isArm64Host
            && (effectiveMachine == (ushort)Machine.Amd64
                || effectiveMachine == (ushort)Machine.I386
                || effectiveMachine == Arm64EcMachine);

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
