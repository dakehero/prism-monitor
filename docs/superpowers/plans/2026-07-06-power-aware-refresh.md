# Power-Aware Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce background energy use on battery by disabling constant tray scanning and refreshing asynchronously on user interaction.

**Architecture:** Add a small core policy that maps power source and main-window visibility to background refresh behavior. The WinUI app owns power-state detection and timer scheduling; tray hover/right-click request asynchronous refreshes without blocking WndProc or menu display.

**Tech Stack:** .NET 10, C#, WinUI 3, MSTest, Windows power status APIs.

---

### Task 1: Add Refresh Policy

**Files:**
- Create: `src/PrismMonitor.Core/Power/PowerSource.cs`
- Create: `src/PrismMonitor.Core/Power/RefreshMode.cs`
- Create: `src/PrismMonitor.Core/Power/RefreshSchedulePolicy.cs`
- Test: `tests/PrismMonitor.Core.Tests/RefreshSchedulePolicyTests.cs`

- [x] **Step 1: Write failing tests**

Add tests proving that AC power allows background refresh, battery power suppresses background refresh while hidden, and battery power still refreshes while the main window is visible.

- [x] **Step 2: Run tests and verify failure**

Run: `dotnet test tests/PrismMonitor.Core.Tests/PrismMonitor.Core.Tests.csproj --configuration Release --no-restore --filter RefreshSchedulePolicyTests`

- [x] **Step 3: Implement minimal policy**

Add the three core files and pass the tests.

- [x] **Step 4: Run tests and verify pass**

Run the same filtered command and confirm the new tests pass.

### Task 2: Add App Power Detection And Timer Scheduling

**Files:**
- Create: `src/PrismMonitor.App/Power/PowerStatusProvider.cs`
- Modify: `src/PrismMonitor.App/App.xaml.cs`

- [x] **Step 1: Wire the policy into App**

Use `RefreshSchedulePolicy.GetRefreshMode(...)` to start or stop `_notificationTimer`.

- [x] **Step 2: Detect power source**

Use `GetSystemPowerStatus` for current state and `Microsoft.Win32.SystemEvents.PowerModeChanged` to re-evaluate after suspend/resume or AC/battery changes.

- [x] **Step 3: Refresh asynchronously after power changes**

When switching modes or resuming, enqueue `NotificationTimer_Tick` through the dispatcher without blocking the power event thread.

### Task 3: Make Tray Interaction Refreshes Non-Blocking

**Files:**
- Modify: `src/PrismMonitor.App/Tray/ShellTrayIcon.cs`
- Modify: `src/PrismMonitor.App/App.xaml.cs`

- [x] **Step 1: Add a refresh request callback**

Expose an app callback that starts background status refresh with no synchronous wait.

- [x] **Step 2: Update hover and right-click paths**

Hover requests refresh asynchronously. Right-click shows the cached menu immediately and requests refresh for the next interaction.

- [x] **Step 3: Verify**

Run full tests, build the app, and install a local MSIX if build succeeds.
