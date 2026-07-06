# Process Visibility Probe

Experimental console probe for comparing non-elevated process visibility paths on Windows ARM64.

It compares:

- .NET `Process.GetProcesses`
- Toolhelp `CreateToolhelp32Snapshot`
- PSAPI `EnumProcesses`
- NT `NtQuerySystemInformation(SystemProcessInformation)`
- Handle-based enrichment with `PROCESS_QUERY_LIMITED_INFORMATION`

The goal is to validate whether Prism Monitor can show more system-owned processes without requesting administrator elevation. The expected model is:

- enumerate PID/name from snapshot APIs;
- read CPU time from the NT system process snapshot where available;
- enrich path, architecture, icon, and termination support only when a limited-information process handle can be opened.
