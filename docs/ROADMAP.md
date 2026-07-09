# Prism Monitor Roadmap

This document tracks the product direction for Prism Monitor, a Windows ARM64 tray app for observing desktop processes that run through the Windows compatibility layer.

## Principles

- Keep the tray experience lightweight and use the main window for full process details.
- Route the main window, tray tooltip, and Toast notifications through the same filtering rules.
- Use system process data only. Do not add CPU sampling loops.
- Show clear feedback for destructive actions such as ending a process.
- Keep each milestone small enough to verify and release independently.

## v0.3 Main Window Usability

Status: complete.

### Process Icons

Show the executable icon next to each process in the main window.

Acceptance criteria:

- Common x64 and x86 desktop processes display their own icons.
- Processes with unreadable paths fall back gracefully.
- Repeated refreshes do not introduce obvious UI stalls.

### End Process

Add a per-row action for ending a process.

Acceptance criteria:

- Ordinary user processes can be ended from the main window.
- Access denied, protected process, and already-exited cases show visible feedback.
- Failed termination attempts do not crash the app.

### No-Flicker Refresh

Update the existing list in place instead of clearing and rebuilding it.

Acceptance criteria:

- Existing rows update CPU time and metadata in place.
- New processes are inserted and exited processes are removed.
- The UI remains responsive while refreshes are running.

### Tray Tooltip Architecture

Keep the tooltip compact, but append the process architecture to each process name.

Acceptance criteria:

- Tooltip rows use the format `process-name (architecture)`.
- The tooltip stays within the Windows notification area tooltip limit.

## v0.4 Ignore Rules

Status: complete.

Add user-configurable ignore rules.

Acceptance criteria:

- Process-name matching is case-insensitive.
- Users can ignore a process directly from the main window.
- Users can view and remove ignored names.
- Ignore rules persist across app restarts.
- Ignored processes are hidden from the main window, tray tooltip, and Toast notifications.

## v0.5 Toast Notifications

Status: complete.

Notify the user when a new compatibility-mode process appears.

Acceptance criteria:

- Newly detected compatibility-mode processes trigger a Toast.
- Processes already running when Prism Monitor starts do not create a notification burst.
- Ignored processes do not trigger Toast notifications.
- Toast quick actions can end the process or add it to the ignore list.

## v0.6 Main Window History and UI Polish

Status: complete.

Polish the main window settings surface and add durable launch history for compatibility-mode processes.

Acceptance criteria:

- Settings are grouped into Monitoring, Notifications, Permissions, and Data sections.
- Launch history records compatibility-mode launches for 30 days.
- History persists as JSONL events with a rebuilt summary for the main window.
- Filters page uses diff-style updates and does not clear, rebuild, or flicker during process refresh.
- Adding and removing ignored apps remains stable.

## v0.6.1 Toast Navigation

Status: complete.

Make Toast notifications useful as entry points into the main window.

Acceptance criteria:

- Clicking the Toast body opens the main window instead of only dismissing the notification.
- If the notified process is still running, the app opens the Processes page and focuses or highlights that process.
- If the process has exited, the app opens the History page and filters to the notified process name.
- Existing Toast quick actions for ending or ignoring a process keep their current behavior.
- Toast activation handling remains safe when the app is already running, hidden to tray, or cold-started from notification activation.

## v0.7 Low-Power Rules Roadmap

Status: in progress.

v0.7 turns the 0.6 feature set into something that can be left running all day. The product goal is not "more Task Manager"; it is a quiet tray app that detects compatibility-mode apps, records useful context, lets the user suppress noise with predictable rules, and proves that it is not wasting battery.

### Release Principles

- Observe lightly: avoid repeated full process scans and avoid CPU sampling loops.
- Enrich deliberately: read expensive identity data once per process lifetime when possible.
- Filter consistently: Processes, History, tray tooltip/menu, and Toast notifications must share the same rule evaluator.
- Keep common actions one-click: full rule editing should exist, but it should not be the default path for ordinary ignores.
- Verify visually and behaviorally: UI changes need render review, and power changes need runtime evidence.

### Non-goals

- Do not require administrator elevation for the normal Store-facing app.
- Do not build a full task-manager replacement.
- Do not redesign signing, release automation, or Store submission as part of 0.7.
- Do not introduce a heavy telemetry, database, or profiler dependency.

### Architecture Target

The 0.7 data flow should converge on this shape:

```text
Windows process data
  -> lightweight snapshots
  -> enrichment cache
  -> app identity rules
  -> Processes / History / Tray / Toast
```

The lightweight snapshot layer should not know about UI. The enrichment cache should not make suppression decisions. The rule evaluator should be pure enough to test in `PrismMonitor.Core`. WinUI, tray, Toast, and icon extraction remain in `PrismMonitor.App`.

### v0.7.0 Low-Power Snapshot Foundation

Status: landed in main.

This milestone introduced the low-power foundation. Runtime surfaces now consume a shared monitoring snapshot instead of independently forcing full process enumeration, and refresh cadence is power-aware.

Delivered:

- Lightweight process snapshots for PID, process name, cumulative CPU time, start time, and detection time.
- Shared snapshot building for main window refresh, tray tooltip/menu, Toast detection, and launch history recording.
- Short-lived process list caching and concurrent refresh coalescing.
- Power-aware refresh policy:
  - Plugged in or unknown power: responsive periodic refresh.
  - Battery with main window visible: slower periodic refresh.
  - Battery while hidden to tray: interaction-only refresh.

Remaining validation:

- Record a manual battery smoke test with the app hidden to tray.
- Compare CPU delta and working set against the 0.6.1 baseline under the same machine state.
- Confirm tray hover/right-click interactions remain asynchronous in battery mode.

### v0.7.1 Enrichment Cache

Status: landed in main.

This milestone separated cheap process discovery from expensive metadata enrichment.

Delivered:

- PID plus process-start identity cache for architecture, executable path, package identity, publisher identity, and ARM64EC/ARM64X classification.
- Cache invalidation for PID reuse.
- Limited-detail fallback for inaccessible or protected processes.
- Tests for cache hits, invalidation, concurrent refresh behavior, partial metadata, and ARM64EC-related classification.

Remaining validation:

- Confirm a long-running app session does not repeatedly re-read unchanged process metadata.
- Confirm unreadable processes remain visible where Windows permissions allow basic process information.

### v0.7.2 App Rules Workflow

Status: next focus.

The rule engine has landed, but the user-facing workflow still needs to become a real product surface. This is the main remaining 0.7 feature milestone.

Deliverables:

- Rename the current Filters surface to Rules if the final UI copy reads better in render review.
- Show active app rules as first-class items, not as a legacy ignored-name list.
- Let users create a rule from Processes, History, or Rules using the strongest available identity:
  - package identity when available
  - executable path when stable and readable
  - publisher identity when useful
  - process name as the fallback
  - architecture as an optional match field
- Keep the common action simple: `Ignore app` creates an all-surface rule without making the user edit fields.
- Add an advanced edit path for match fields and suppression targets:
  - Processes
  - History
  - Tray
  - Toast
  - All
- Make rule effects visible enough to review: each rule should show what it matches and where it applies.
- Preserve no-flicker list updates for Processes, History, and Rules.

Acceptance criteria:

- Creating a rule from a running process suppresses that app consistently from every selected surface.
- Creating a rule from a history row works even when the process is no longer running.
- Editing suppression targets changes only the selected surfaces.
- Deleting a rule restores matching apps to visible/notifiable surfaces on the next refresh.
- Existing legacy ignored-name data migrates into rules without losing user choices.
- UI render review covers Processes, History, Rules, Settings, tray menu, and Toast activation paths affected by rules.

### v0.7.3 Power Diagnostics and Release Proof

Status: planned.

This milestone closes the gap between "the code should be lower power" and "we can prove it behaves lower power on the target machine."

Deliverables:

- Add a lightweight diagnostics section in Settings or a hidden developer-friendly view showing:
  - current power source
  - current refresh mode
  - main-window refresh interval
  - background refresh interval
  - last successful snapshot time
  - last snapshot duration
  - process count before and after rule filtering
  - enrichment cache hit/miss counts for the current session
- Add a manual smoke-test script or documented command sequence for:
  - plugged-in baseline
  - battery hidden-to-tray behavior
  - battery main-window-visible behavior
- Keep diagnostics local-only. Do not upload telemetry.

Acceptance criteria:

- Hidden-to-tray battery mode shows interaction-only refresh unless the user opens the main window or interacts with the tray.
- The diagnostics view explains the active refresh mode without requiring a debugger.
- A local smoke test records CPU delta, working set, power source, refresh mode, and process path/version.
- Diagnostics do not add a new polling loop or measurable idle overhead.

### v0.7.4 UI Fit and Finish

Status: planned.

This milestone is for polishing the visible experience after the Rules workflow and diagnostics exist.

Deliverables:

- Make Processes, History, and Rules feel like one app:
  - compact default rows
  - smooth expand/collapse details
  - direct copy affordances for PID, path, package identity, and publisher identity
  - clear empty states
  - visible scroll paths
- Keep details unframed unless a real nested tool needs a boundary.
- Avoid table-like equal-width columns where content length varies heavily.
- Keep tray tooltip compact and use the main window for detail.

Acceptance criteria:

- No obvious row/header misalignment in the default window size.
- Long paths and package identities remain inspectable and copyable.
- The main window remains singleton-like and activates quickly from tray and Toast.
- UI render review is attached or summarized for each affected screen.

### Release Gates

v0.7 is not complete until both power behavior and rule consistency are proven.

- Core tests pass for snapshot coordination, enrichment cache, rule migration, rule matching, target-specific suppression, and partial metadata.
- App build passes for `win-arm64` Release.
- Manual smoke test records plugged-in and battery behavior, including CPU delta, working set, refresh mode, and whether the main window was visible.
- Rule workflow is verified across Processes, History, tray tooltip/menu, and Toast notifications.
- Manual render review is done for every visible UI change, using XAML Live Preview when available or a launched local build screenshot.
- No generated MSIX artifacts, package output, local logs, certificates, or runtime data files are committed.

### 0.8 Candidates

These ideas are intentionally deferred:

- More advanced notification policy presets.
- Store-ready packaging/signing improvements.
- Localization resources after the English baseline stabilizes.
- A richer diagnostics page if the lightweight v0.7 diagnostics prove useful.

## Future Ideas

- Command-line render-review harness for WinUI UI changes.
