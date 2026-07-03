# Native Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Do not create git commits; the user's AGENTS.md instruction says commits must not be created unless explicitly requested.

**Goal:** Build a .NET 10 C# WinUI 3 tray app for Windows ARM64 that lists non-native x64/x86 processes and their cumulative CPU time without CPU sampling.

**Architecture:** Put testable process classification, CPU-time formatting, tooltip formatting, and service composition into a core library. Keep Win32 process enumeration and interop behind small interfaces. Keep the WinUI 3 project focused on tray icon lifetime, popup UI, and calls into the core service.

**Tech Stack:** .NET 10, C#, WinUI 3 / Windows App SDK, MSTest, Win32 P/Invoke, `win-arm64`, Native AOT publish attempt with self-contained fallback.

---

## File Structure

- `NativeGuard.sln`: solution containing app, core library, and tests.
- `src/NativeGuard.Core/NativeGuard.Core.csproj`: testable non-UI library.
- `src/NativeGuard.Core/Processes/MachineType.cs`: process/native machine constants and display names.
- `src/NativeGuard.Core/Processes/ProcessArchitectureClassifier.cs`: classifies `IsWow64Process2` machine values.
- `src/NativeGuard.Core/Processes/CpuTimeFormatter.cs`: formats cumulative CPU time.
- `src/NativeGuard.Core/Processes/NonNativeProcessInfo.cs`: immutable process row model.
- `src/NativeGuard.Core/Processes/TrayTooltipFormatter.cs`: Top N tooltip formatting.
- `src/NativeGuard.Core/Processes/IProcessInfoProvider.cs`: abstraction for process snapshots.
- `src/NativeGuard.Core/Processes/NonNativeProcessService.cs`: sorts and returns non-native process rows.
- `src/NativeGuard.App/NativeGuard.App.csproj`: WinUI 3 tray app.
- `src/NativeGuard.App/App.xaml`, `src/NativeGuard.App/App.xaml.cs`: WinUI app startup.
- `src/NativeGuard.App/MainWindow.xaml`, `src/NativeGuard.App/MainWindow.xaml.cs`: compact popup table.
- `src/NativeGuard.App/Interop/NativeMethods.cs`: P/Invoke declarations.
- `src/NativeGuard.App/Processes/Win32ProcessInfoProvider.cs`: real Windows process provider.
- `src/NativeGuard.App/Tray/TrayIcon.cs`: notification area icon wrapper.
- `src/NativeGuard.App/app.manifest`: administrator elevation manifest.
- `tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj`: MSTest project.
- `tests/NativeGuard.Core.Tests/ProcessArchitectureClassifierTests.cs`: classification tests.
- `tests/NativeGuard.Core.Tests/CpuTimeFormatterTests.cs`: formatting tests.
- `tests/NativeGuard.Core.Tests/TrayTooltipFormatterTests.cs`: tooltip tests.
- `tests/NativeGuard.Core.Tests/NonNativeProcessServiceTests.cs`: service sorting/failure tests.

## Task 1: Scaffold Solution

**Files:**
- Create: `NativeGuard.sln`
- Create: `src/NativeGuard.Core/NativeGuard.Core.csproj`
- Create: `src/NativeGuard.App/NativeGuard.App.csproj`
- Create: `tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n NativeGuard
dotnet new classlib -n NativeGuard.Core -o src/NativeGuard.Core -f net10.0
dotnet new mstest -n NativeGuard.Core.Tests -o tests/NativeGuard.Core.Tests -f net10.0
dotnet new winui -n NativeGuard.App -o src/NativeGuard.App -f net10.0
dotnet sln NativeGuard.sln add src/NativeGuard.Core/NativeGuard.Core.csproj
dotnet sln NativeGuard.sln add src/NativeGuard.App/NativeGuard.App.csproj
dotnet sln NativeGuard.sln add tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj
dotnet add tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj reference src/NativeGuard.Core/NativeGuard.Core.csproj
dotnet add src/NativeGuard.App/NativeGuard.App.csproj reference src/NativeGuard.Core/NativeGuard.Core.csproj
```

Expected: all projects are created and added to the solution.

- [ ] **Step 2: Remove template placeholder files**

Delete template `Class1.cs` and `UnitTest1.cs` after project creation.

- [ ] **Step 3: Build scaffold**

Run:

```powershell
dotnet build NativeGuard.sln
```

Expected: the solution builds, or any WinUI template issue is captured before code is added.

## Task 2: Architecture Classification

**Files:**
- Create: `src/NativeGuard.Core/Processes/MachineType.cs`
- Create: `src/NativeGuard.Core/Processes/ProcessArchitectureClassifier.cs`
- Create: `tests/NativeGuard.Core.Tests/ProcessArchitectureClassifierTests.cs`

- [ ] **Step 1: Write failing tests**

Create tests that assert:

- ARM64 process on ARM64 host is native.
- AMD64 process on ARM64 host is non-native and displays `x64`.
- I386 process on ARM64 host is non-native and displays `x86`.
- Unknown process machine is not treated as non-native.

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter ProcessArchitectureClassifierTests
```

Expected: fail because the classifier does not exist.

- [ ] **Step 2: Implement minimal classifier**

Implement `MachineType` constants for `Unknown`, `I386`, `Amd64`, and `Arm64`, then implement a classifier that returns `ProcessArchitectureInfo` with `IsNonNative` and `DisplayName`.

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter ProcessArchitectureClassifierTests
```

Expected: pass.

## Task 3: CPU Time Formatting

**Files:**
- Create: `src/NativeGuard.Core/Processes/CpuTimeFormatter.cs`
- Create: `tests/NativeGuard.Core.Tests/CpuTimeFormatterTests.cs`

- [ ] **Step 1: Write failing tests**

Create tests for:

- `TimeSpan.Zero` formats as `0s`.
- 59 seconds formats as `59s`.
- 61 seconds formats as `1m 01s`.
- 3661 seconds formats as `1h 01m 01s`.

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter CpuTimeFormatterTests
```

Expected: fail because the formatter does not exist.

- [ ] **Step 2: Implement minimal formatter**

Implement a static formatter that rounds down to whole seconds and emits compact fixed-width minute/second fields after hours or minutes appear.

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter CpuTimeFormatterTests
```

Expected: pass.

## Task 4: Tooltip Formatting

**Files:**
- Create: `src/NativeGuard.Core/Processes/NonNativeProcessInfo.cs`
- Create: `src/NativeGuard.Core/Processes/TrayTooltipFormatter.cs`
- Create: `tests/NativeGuard.Core.Tests/TrayTooltipFormatterTests.cs`

- [ ] **Step 1: Write failing tests**

Create tests that assert:

- Empty process list returns `Native Guard: no non-native processes`.
- Top N processes are sorted by cumulative CPU time descending.
- Tooltip includes process name, PID, architecture, and formatted CPU time.

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter TrayTooltipFormatterTests
```

Expected: fail because the model and formatter do not exist.

- [ ] **Step 2: Implement minimal model and formatter**

Implement `NonNativeProcessInfo` as an immutable record and `TrayTooltipFormatter.FormatTopProcesses`.

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter TrayTooltipFormatterTests
```

Expected: pass.

## Task 5: Non-Native Process Service

**Files:**
- Create: `src/NativeGuard.Core/Processes/IProcessInfoProvider.cs`
- Create: `src/NativeGuard.Core/Processes/NonNativeProcessService.cs`
- Create: `tests/NativeGuard.Core.Tests/NonNativeProcessServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create tests that assert:

- The service returns rows sorted by CPU time descending.
- The service returns an empty list when the provider returns no rows.
- A provider exception returns an empty list rather than crashing the UI layer.

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter NonNativeProcessServiceTests
```

Expected: fail because the service does not exist.

- [ ] **Step 2: Implement minimal service**

Implement an async service that calls `IProcessInfoProvider.GetNonNativeProcessesAsync`, catches provider exceptions, and returns sorted immutable results.

- [ ] **Step 3: Run tests**

Run:

```powershell
dotnet test tests/NativeGuard.Core.Tests/NativeGuard.Core.Tests.csproj --filter NonNativeProcessServiceTests
```

Expected: pass.

## Task 6: Win32 Process Provider

**Files:**
- Create: `src/NativeGuard.App/Interop/NativeMethods.cs`
- Create: `src/NativeGuard.App/Processes/Win32ProcessInfoProvider.cs`
- Modify: `src/NativeGuard.App/NativeGuard.App.csproj`

- [ ] **Step 1: Implement isolated interop**

Use P/Invoke for:

- `EnumProcesses` from `psapi.dll`.
- `OpenProcess` from `kernel32.dll`.
- `CloseHandle` from `kernel32.dll`.
- `IsWow64Process2` from `kernel32.dll`.
- `GetProcessTimes` from `kernel32.dll`.
- `QueryFullProcessImageName` from `kernel32.dll`.

Use minimal process access flags:

- `PROCESS_QUERY_LIMITED_INFORMATION`

- [ ] **Step 2: Implement provider**

For each process ID:

- Open the process with limited query access.
- Read architecture with `IsWow64Process2`.
- Use `ProcessArchitectureClassifier` to skip native processes.
- Read CPU time with `GetProcessTimes`.
- Read image path with `QueryFullProcessImageName`.
- Use the file name without extension as the display name.
- Skip failures per process.

- [ ] **Step 3: Build app project**

Run:

```powershell
dotnet build src/NativeGuard.App/NativeGuard.App.csproj
```

Expected: builds, or reports concrete WinUI/interop issues.

## Task 7: Tray UI

**Files:**
- Modify: `src/NativeGuard.App/App.xaml`
- Modify: `src/NativeGuard.App/App.xaml.cs`
- Modify: `src/NativeGuard.App/MainWindow.xaml`
- Modify: `src/NativeGuard.App/MainWindow.xaml.cs`
- Create: `src/NativeGuard.App/Tray/TrayIcon.cs`
- Create: `src/NativeGuard.App/app.manifest`

- [ ] **Step 1: Add app manifest**

Set requested execution level to `requireAdministrator`.

- [ ] **Step 2: Add tray icon wrapper**

Use `Shell_NotifyIcon` with:

- `NIM_ADD`
- `NIM_MODIFY`
- `NIM_DELETE`
- `NIF_MESSAGE`
- `NIF_ICON`
- `NIF_TIP`

The tray icon updates tooltip text when the pointer hovers or before display.

- [ ] **Step 3: Add WinUI popup**

Build a compact window with a table-like list showing:

- Process
- PID
- Arch
- CPU Time

Add a refresh button that reloads once on demand.

- [ ] **Step 4: Wire service to UI**

Instantiate `NonNativeProcessService` with `Win32ProcessInfoProvider`, refresh on popup open, and use `TrayTooltipFormatter` for tooltip text.

- [ ] **Step 5: Build app**

Run:

```powershell
dotnet build src/NativeGuard.App/NativeGuard.App.csproj
```

Expected: builds.

## Task 8: Publish and Verification

**Files:**
- Modify: `src/NativeGuard.App/NativeGuard.App.csproj` if publish properties need adjustment.

- [ ] **Step 1: Run all tests**

Run:

```powershell
dotnet test NativeGuard.sln
```

Expected: all tests pass.

- [ ] **Step 2: Build solution**

Run:

```powershell
dotnet build NativeGuard.sln
```

Expected: build succeeds.

- [ ] **Step 3: Try Native AOT publish**

Run:

```powershell
dotnet publish src/NativeGuard.App/NativeGuard.App.csproj -c Release -r win-arm64 --self-contained true -p:PublishAot=true
```

Expected: either publish succeeds, or failure is recorded as a WinUI/Windows App SDK Native AOT limitation.

- [ ] **Step 4: Publish self-contained fallback if needed**

Run only if Native AOT publish fails:

```powershell
dotnet publish src/NativeGuard.App/NativeGuard.App.csproj -c Release -r win-arm64 --self-contained true
```

Expected: publish succeeds.

- [ ] **Step 5: Manual smoke test**

Run the published app as administrator on Windows ARM64 and verify:

- A tray icon appears.
- Clicking it opens the process popup.
- The popup lists only non-native accessible processes.
- CPU time is cumulative, not a sampled percentage.
- Hover tooltip shows Top N by cumulative CPU time.
- Closing the popup leaves the tray app running.
- Exiting from the tray removes the icon.

## Self-Review

Spec coverage:

- WinUI 3 tray app: Task 7.
- .NET 10 C#: Task 1.
- Native AOT preferred with fallback: Task 8.
- Administrator elevation: Task 7.
- No CPU sampling: Task 5 and Task 6 use cumulative CPU time only.
- Non-native detection with `IsWow64Process2`: Task 2 and Task 6.
- CPU time with public API: Task 3 and Task 6.
- Popup and tooltip: Task 4 and Task 7.
- Best-effort process failures: Task 5 and Task 6.

Placeholder scan:

- No `TODO` or `TBD` items are intentionally left in the plan.

Type consistency:

- `NonNativeProcessInfo`, `ProcessArchitectureClassifier`, `CpuTimeFormatter`, `TrayTooltipFormatter`, `IProcessInfoProvider`, and `NonNativeProcessService` are introduced before later tasks reference them.
