# Process Visibility Probe

Date: 2026-07-07

Branch: `codex/process-visibility-probe`

## Goal

Validate whether Prism Monitor can show more system-owned processes without administrator elevation.

The specific hypothesis was that snapshot-style process enumeration can expose PID, process name, and CPU time even when `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` fails. Path, architecture, icon, and termination support can then remain best-effort enrichment.

## Probe

The experimental tool lives at `tools/ProcessVisibilityProbe`.

It compares:

- .NET `Process.GetProcesses`
- Toolhelp `CreateToolhelp32Snapshot`
- PSAPI `EnumProcesses`
- NT `NtQuerySystemInformation(SystemProcessInformation)`
- handle-based enrichment with `PROCESS_QUERY_LIMITED_INFORMATION`
- `GetProcessInformation(ProcessMachineTypeInfo)` architecture enrichment

## Result

Run context:

- Elevated: `False`
- OS: `Microsoft Windows NT 10.0.28000.0`
- Process architecture: `Arm64`
- OS architecture: `Arm64`

Observed counts:

| Source | Count |
| --- | ---: |
| Toolhelp names | 383 |
| NT names | 383 |
| NT CPU times | 383 |
| PSAPI PIDs | 383 |
| .NET names | 383 |
| .NET CPU readable | 206 |
| Union PIDs | 383 |
| `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` | 206 |
| `QueryFullProcessImageName` | 206 |
| `IsWow64Process2` | 206 |
| `GetProcessInformation(ProcessMachineTypeInfo)` | 191-206 |
| `GetProcessTimes` | 206 |
| `OpenProcess` denied | 176 |

The second architecture run was taken after the live process set changed:

| Source | Count |
| --- | ---: |
| Union PIDs | 370 |
| `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` | 191 |
| `IsWow64Process2` | 191 |
| `GetProcessInformation(ProcessMachineTypeInfo)` | 191 |
| `OpenProcess` denied | 177 |

Representative processes visible without an openable limited-information handle:

- `System`
- `Secure System`
- `Registry`
- `smss.exe`
- `csrss.exe`
- `wininit.exe`
- `winlogon.exe`
- multiple `svchost.exe` instances

## Conclusion

The snapshot approach works for the main product goal of improving visibility without administrator elevation.

For process architecture specifically, there does not appear to be a documented no-handle API that returns the architecture of an arbitrary running process. Both tested official architecture APIs require a process handle with limited query access:

- `IsWow64Process2`
- `GetProcessInformation(ProcessMachineTypeInfo)`

For processes that cannot be opened without administrator rights or debug privilege, architecture should remain `Unknown` unless it can be inferred from a trustworthy executable path.

`ProcessMachineTypeInfo` also does not replace PE inspection for ARM64EC / ARM64X detection. A follow-up run printed accessible images with ARM64X-like section metadata:

| Process | Path family | `ProcessMachine` | `MachineAttributes` |
| --- | --- | ---: | ---: |
| `cmd.exe` | `C:\Windows\System32` | `0xaa64` | `0x000003` |
| `dllhost.exe` | `C:\Windows\System32` | `0xaa64` | `0x000003` |
| `audiodg.exe` | `C:\Windows\System32` | `0xaa64` | `0x000003` |
| `olkexthostcompat.exe` | Outlook WindowsApps package | `0x8664` | `0x000001` |

This suggests `ProcessMachineTypeInfo` reports the running process machine/persona (`ARM64` or `AMD64`) rather than the full hybrid PE identity. A hybrid image may run as ARM64 or as an x64-compatible persona depending on activation/context. Therefore the app still needs PE metadata inspection when a path is available and it wants to distinguish plain x64 emulation from ARM64EC / ARM64X.

## Apple Music Case Study

Apple Music was running from:

`C:\Program Files\WindowsApps\AppleInc.AppleMusicWin_1.1540.23042.0_arm64__nzyj5cx40ttqa`

Observed process values:

| Process | `ProcessMachineTypeInfo.ProcessMachine` | `MachineAttributes` |
| --- | ---: | ---: |
| `AppleMusic.exe` | `0x8664` | `0x000001` |
| `AMPLibraryAgent.exe` | `0x8664` | `0x000001` |

Direct PE parsing for both executables:

| File | COFF machine | Hybrid sections |
| --- | ---: | --- |
| `AppleMusic.exe` | `0x8664` | `.hexpthk`, `.a64xrm` |
| `AMPLibraryAgent.exe` | `0x8664` | `.hexpthk`, `.a64xrm` |

This explains why Apple Music can be misclassified as plain x64 if the app only reads process-machine values. For x64-compatible processes with a readable executable path, Prism Monitor should inspect PE sections even when `ProcessMachineTypeInfo` succeeds.

Recommended product model:

1. Enumerate all visible processes through a snapshot source.
2. Use NT `SystemProcessInformation` as the CPU-time source when available.
3. Enrich path, architecture, icon, and termination capability only when a limited-information process handle can be opened.
4. Keep inaccessible processes in the UI with degraded fields instead of dropping them.

This would allow Prism Monitor to show system-owned processes with name and CPU time while keeping Microsoft Store packaging free of `allowElevation`.

## Caveats

- Architecture cannot be read for processes whose handle cannot be opened.
- `GetProcessInformation(ProcessMachineTypeInfo)` does not bypass the handle boundary.
- `GetProcessInformation(ProcessMachineTypeInfo)` does not by itself distinguish plain x64 from ARM64EC / ARM64X; PE metadata remains useful.
- Process path and icon cannot be read for those inaccessible processes.
- Termination should remain disabled for inaccessible processes.
- NT `NtQuerySystemInformation` is not WinRT. It is lower-level than Toolhelp/PSAPI, so Store review risk should be weighed against the user value.
