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
| `GetProcessTimes` | 206 |
| `OpenProcess` denied | 176 |

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

Recommended product model:

1. Enumerate all visible processes through a snapshot source.
2. Use NT `SystemProcessInformation` as the CPU-time source when available.
3. Enrich path, architecture, icon, and termination capability only when a limited-information process handle can be opened.
4. Keep inaccessible processes in the UI with degraded fields instead of dropping them.

This would allow Prism Monitor to show system-owned processes with name and CPU time while keeping Microsoft Store packaging free of `allowElevation`.

## Caveats

- Architecture cannot be read for processes whose handle cannot be opened.
- Process path and icon cannot be read for those inaccessible processes.
- Termination should remain disabled for inaccessible processes.
- NT `NtQuerySystemInformation` is not WinRT. It is lower-level than Toolhelp/PSAPI, so Store review risk should be weighed against the user value.
