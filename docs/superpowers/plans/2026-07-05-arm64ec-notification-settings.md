# ARM64EC Notification Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Distinguish ARM64EC/ARM64X processes from plain x64, allow users to exclude them, and gate Toast notifications by architecture level.

**Architecture:** Keep architecture classification in `PrismMonitor.Core`, add small filtering/settings types there, and wire them into the WinUI app through a JSON settings file under `%LocalAppData%\PrismMonitor`. UI settings are simple immediate-save controls in the main window.

**Tech Stack:** .NET 10, C#, WinUI 3, MSTest, source-generated `System.Text.Json`.

---

### Task 1: Architecture Classification

**Files:**
- Modify: `src/PrismMonitor.Core/Processes/ProcessArchitectureClassifier.cs`
- Test: `tests/PrismMonitor.Core.Tests/ProcessArchitectureClassifierTests.cs`

- [ ] Add tests that ARM64EC (`0xa641`) and ARM64X (`0xa64e`) are classified as hybrid compatibility processes with distinct display names.
- [ ] Add a test that `Unknown` process machine falls back to ARM64X PE machine.
- [ ] Implement the ARM64X constant and classification.
- [ ] Run `dotnet test PrismMonitor.slnx --configuration Release --no-restore --filter ProcessArchitectureClassifierTests`.

### Task 2: Monitoring Settings Model

**Files:**
- Create: `src/PrismMonitor.Core/Settings/MonitoringSettings.cs`
- Create: `src/PrismMonitor.Core/Settings/MonitoringSettingsStore.cs`
- Test: `tests/PrismMonitor.Core.Tests/MonitoringSettingsStoreTests.cs`

- [ ] Add tests for default settings when the file is missing.
- [ ] Add tests that settings persist and invalid JSON falls back to defaults.
- [ ] Implement `IncludeArm64EcProcesses`, `NotificationLevel`, and source-generated JSON serialization.
- [ ] Run `dotnet test PrismMonitor.slnx --configuration Release --no-restore --filter MonitoringSettingsStoreTests`.

### Task 3: Architecture Filtering

**Files:**
- Create: `src/PrismMonitor.Core/Processes/ArchitectureProcessFilter.cs`
- Test: `tests/PrismMonitor.Core.Tests/ArchitectureProcessFilterTests.cs`

- [ ] Add tests that ARM64EC/ARM64X can be excluded from visible process lists.
- [ ] Add tests for `X86Only`, `X86AndX64`, and `X86X64AndArm64Ec` notification levels.
- [ ] Implement architecture-name based filtering using the existing `CompatibilityProcessInfo.Architecture` field.
- [ ] Run `dotnet test PrismMonitor.slnx --configuration Release --no-restore --filter ArchitectureProcessFilterTests`.

### Task 4: App Integration

**Files:**
- Modify: `src/PrismMonitor.App/App.xaml.cs`
- Modify: `src/PrismMonitor.App/MainWindow.xaml`
- Modify: `src/PrismMonitor.App/MainWindow.xaml.cs`

- [ ] Add a `MonitoringSettingsStore` next to the ignored-process store.
- [ ] Apply ARM64EC visibility filtering to main window, tray tooltip, and Toast scanning.
- [ ] Apply notification-level filtering before showing Toasts.
- [ ] Add main-window controls for including ARM64EC and selecting notification level.
- [ ] Run `dotnet test PrismMonitor.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet build src/PrismMonitor.App/PrismMonitor.App.csproj --configuration Release --no-restore`.

### Task 5: Commit

**Files:**
- All modified files above.

- [ ] Check `git status --short`.
- [ ] Commit with `feat: add ARM64EC notification settings`.
