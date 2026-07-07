# WinUI List Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish the Processes and History pages into a calmer WinUI list/details experience.

**Architecture:** Keep the existing `ListView` + `Expander` implementation and observable row updates. Add page-level status text, shared Fluent-inspired row resources, compact commands, inline detail rows, and direct process actions without changing process detection or history storage behavior.

**Tech Stack:** .NET 10, C#, WinUI 3, Windows App SDK, ARM64 Release build.

## Global Constraints

- Do not add a third-party table/list dependency.
- Do not reintroduce table headers or equal-width pseudo-table columns.
- Do not add new background polling or process sampling.
- Use system theme resources and existing row models.
- Every visible UI change needs a real render review before handoff.

---

### Task 1: Layout Regression Tests

**Files:**
- Modify: `tests/PrismMonitor.Core.Tests/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: `src/PrismMonitor.App/MainWindow.xaml`
- Produces: tests that require polished WinUI list affordances.

- [x] **Step 1: Write the failing test**

Add `ProcessesAndHistoryUseFluentListPresentation()` asserting:
- `ProcessStatusTextBlock` and `HistoryStatusTextBlock` exist.
- `ProcessHeader` and `HistoryHeader` exist.
- `HistoryToolbar` exists.
- row templates use `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, and `CornerRadius="8"`.
- `ProcessDetailsPanel` and `HistoryDetailsPanel` exist.
- refresh, copy, and actions commands expose `ToolTipService.ToolTip`.

- [x] **Step 2: Run test and verify failure**

```powershell
dotnet test tests/PrismMonitor.Core.Tests/PrismMonitor.Core.Tests.csproj --configuration Release --no-restore --filter MainWindowLayoutTests.ProcessesAndHistoryUseFluentListPresentation
```

Expected: FAIL because the current XAML lacks the new status, toolbar, card, and details affordances.

### Task 2: Fluent Page Headers and Status

**Files:**
- Modify: `src/PrismMonitor.App/MainWindow.xaml`
- Modify: `src/PrismMonitor.App/MainWindow.xaml.cs`

**Interfaces:**
- Produces: `ProcessStatusTextBlock` and `HistoryStatusTextBlock` updated from existing row collections.

- [x] **Step 1: Add headers**

Add page header grids with title, status text, and icon-only refresh button.

- [x] **Step 2: Update status text in code**

After process and history snapshots are applied, set concise status text such as `2 active processes` and `12 history entries`.

- [x] **Step 3: Run targeted tests**

Run the same layout test filter and keep implementing until it passes after Task 3.

### Task 3: Row Backplates, Badges, Details Panels, and Icon Commands

**Files:**
- Modify: `src/PrismMonitor.App/MainWindow.xaml`

**Interfaces:**
- Consumes: existing `ProcessRow` and `HistoryRow` properties.
- Produces: polished row templates with the same data, inline copy values, and direct process commands.

- [x] **Step 1: Add shared resources**

Add XAML resources for section titles, caption text, row cards, architecture badges, inline copy values, and compact icon command buttons.

- [x] **Step 2: Polish process rows**

Wrap rows in rounded backplates, keep expanded metadata inline, make PID directly copyable, and expose direct `End process` and `Ignore` buttons.

- [x] **Step 3: Polish history rows**

Mirror the process treatment for `HistoryDetailsPanel`, row cards, badges, and direct PID copy values. Keep paths selectable instead of adding repeated copy buttons.

- [x] **Step 4: Run targeted tests**

```powershell
dotnet test tests/PrismMonitor.Core.Tests/PrismMonitor.Core.Tests.csproj --configuration Release --no-restore --filter MainWindowLayoutTests
```

Expected: PASS.

### Task 4: Build, Render Review, Commit, Push

**Files:**
- Commit code, tests, spec, and plan only.

**Interfaces:**
- Produces: verified and pushed branch update.

- [x] **Step 1: Run full tests**

```powershell
dotnet test PrismMonitor.slnx --configuration Release --no-restore
```

- [x] **Step 2: Run ARM64 Release build**

```powershell
Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:MSBuildSDKsPath -ErrorAction SilentlyContinue
dotnet build src/PrismMonitor.App/PrismMonitor.App.csproj --configuration Release --runtime win-arm64 --self-contained true --no-restore
```

- [x] **Step 3: Install or launch and render-review**

Use the current local MSIX flow or direct app launch. Capture Processes and History default and expanded states, checking alignment, spacing, clipping, scrollbars, and command affordances.

- [x] **Step 4: Commit and push**

```powershell
git add docs/superpowers/specs/2026-07-08-winui-list-polish-design.md docs/superpowers/plans/2026-07-08-winui-list-polish.md src/PrismMonitor.App/MainWindow.xaml src/PrismMonitor.App/MainWindow.xaml.cs tests/PrismMonitor.Core.Tests/MainWindowLayoutTests.cs
git commit -m "style: polish process and history lists"
git push origin codex/0.6-history-ui
```
