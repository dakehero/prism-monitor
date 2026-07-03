namespace NativeGuard.Core.Processes;

public enum MachineType : ushort
{
    Unknown = 0x0000,
    I386 = 0x014c,
    Amd64 = 0x8664,
    Arm64 = 0xaa64
}
