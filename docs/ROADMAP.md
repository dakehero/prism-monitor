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

## v0.7 Low-Power Rules Release

Status: active on `codex/0.7-low-power-snapshot`.

v0.7 is the release that makes Prism Monitor credible as an always-on tray app. It should detect compatibility-mode apps, remember useful launch context, suppress noise with understandable app rules, and provide enough local evidence that the app is not wasting battery.

### Release Contract

- The app remains lightweight: no CPU sampling loops, no profiler dependency, and no full task-manager scope.
- One monitoring path feeds Processes, History, tray tooltip/menu, and Toast notifications.
- Expensive process enrichment is cached by PID plus process start time.
- Rules are app-identity based, not only process-name based.
- The common path is one click: `Ignore app`.
- Advanced rule editing exists for users who need target-specific suppression.
- UI work must receive a real render review before handoff.
- Power behavior must be verified with local runtime evidence before release.

### Architecture Shape

```text
Windows process data
  -> low-power snapshot coordinator
  -> process enrichment cache
  -> app identity rule evaluator
  -> Processes / History / Tray / Toast
```

`PrismMonitor.Core` owns testable models, rules, history, and monitoring policy. `PrismMonitor.App` owns WinUI, tray, Toast, icons, app lifetime, process interop, and packaging.

### v0.7.0 Low-Power Snapshot Foundation

Status: landed in `main`.

Scope:

- Shared snapshot path for main window refresh, tray tooltip/menu, Toast detection, and history recording.
- Short-lived process list cache.
- Concurrent refresh coalescing.
- Power-aware refresh policy:
  - Plugged in or unknown power: responsive periodic refresh.
  - Battery with main window visible: slower periodic refresh.
  - Battery while hidden to tray: interaction-driven refresh.

Exit criteria:

- Core tests cover snapshot coordination and refresh coalescing.
- App build passes for `win-arm64` Release.
- Tray hover and right-click refresh paths remain asynchronous.

Follow-up proof is tracked in v0.7.3.

### v0.7.1 Enrichment Cache

Status: landed in `main`.

Scope:

- Cache process architecture, executable path, package identity, publisher identity, and ARM64EC/ARM64X classification.
- Invalidate cache on PID reuse by using process start time.
- Keep partial metadata when protected or inaccessible processes cannot be fully read.

Exit criteria:

- Tests cover cache hit, invalidation, concurrent refresh, partial metadata, and ARM64EC-related classification.
- Long-running sessions do not repeatedly re-read unchanged identity metadata.

Follow-up proof is tracked in v0.7.3.

### v0.7.2 App Rules Workflow

Status: in progress on this branch.

Goal: replace the legacy ignored-name workflow with a real app rules surface.

Scope:

- Show `Rules` as a first-class main window view.
- Display active rules with:
  - display name
  - match summary
  - suppression targets
  - remove action
- Create rules from:
  - a running process
  - a history row
  - the Rules page
- Prefer the strongest available identity:
  - package identity
  - executable path
  - publisher identity
  - process name
  - architecture only as a qualifier, never as the only match field
- Support target-specific suppression:
  - All
  - Processes
  - History
  - Tray
  - Toast
- Preserve migration from legacy ignored process names.
- Preserve no-flicker updates in Processes, History, and Rules.

Current branch state:

- Rule store add/update/remove APIs are implemented.
- `Ignore app` actions from Processes and History create app identity rules.
- Rules view replaces the old Filters view.
- Rules can be added with a chosen suppression target and removed.
- Architecture-only rules are rejected so one broad architecture value cannot hide many unrelated apps.

Remaining before v0.7.2 is done:

- Add an edit path for existing rules, at minimum target changes and display/match review.
- Verify rule effects across Processes, History, tray tooltip/menu, and Toast notification paths.
- Complete render review for Processes, History, Rules, Settings, tray menu, and Toast activation surfaces affected by rules.
- Run full tests and `win-arm64` Release build after the final UI pass.

Exit criteria:

- Creating a rule from a running process suppresses that app from every selected surface.
- Creating a rule from a history row works after the process has exited.
- Editing suppression targets changes only the selected surfaces.
- Deleting a rule restores matching apps on the next refresh.
- Legacy ignored-name data migrates without losing user choices.
- Render review evidence is captured or the exact blocker is documented.

### v0.7.3 Power Diagnostics and Release Proof

Status: planned after v0.7.2.

Goal: prove the low-power architecture on the target machine instead of relying on intent.

Scope:

- Add local diagnostics in Settings or a developer-friendly diagnostics view:
  - power source
  - active refresh mode
  - foreground and background refresh intervals
  - last snapshot time and duration
  - process count before and after rules
  - enrichment cache hit/miss counts for the current session
- Add a manual smoke-test script or documented sequence for:
  - plugged-in baseline
  - battery with main window visible
  - battery hidden to tray
- Keep diagnostics local-only and avoid introducing a new polling loop.

Exit criteria:

- Battery hidden-to-tray mode shows interaction-driven refresh.
- Diagnostics explain the active refresh mode without a debugger.
- A local smoke record captures CPU delta, working set, power source, refresh mode, app version/path, and visibility state.
- Diagnostics have no measurable idle overhead beyond the existing monitoring path.

### v0.7.4 UI Fit and Finish

Status: planned after v0.7.3 unless v0.7.2 render review exposes release-blocking UI issues.

Goal: make the visible experience feel coherent after the rules and diagnostics surfaces exist.

Scope:

- Align Processes, History, Rules, and Settings around the same list-detail design language.
- Keep default rows compact.
- Keep expanded details unframed unless a real nested tool needs a boundary.
- Make long paths, package identities, publisher identities, and PIDs easy to inspect and copy.
- Keep empty states clear and scroll paths visible.
- Avoid equal-width table layouts where content length varies heavily.

Exit criteria:

- No obvious row/header misalignment at the default window size.
- Long paths and identities remain inspectable and copyable.
- The main window remains singleton-like and activates quickly from tray and Toast.
- Render review is completed for every changed surface.

### Release Gates

v0.7 is ready to tag only when all of these are true:

- Core tests pass for snapshots, enrichment cache, ARM64EC classification, rule migration, rule matching, target-specific suppression, and partial metadata.
- App build passes for `win-arm64` Release.
- Rules are manually verified across Processes, History, tray tooltip/menu, and Toast notifications.
- Battery and plugged-in smoke evidence is recorded.
- UI render review is complete for all changed visible surfaces.
- No generated MSIX artifacts, local logs, certificates, package output, or runtime data files are committed.

### Deferred to v0.8+

- Store-ready signing and release automation improvements.
- Rich notification policy presets.
- Localization after the English baseline stabilizes.
- A larger diagnostics dashboard if the v0.7 local diagnostics prove useful.

## Future Ideas

- Command-line render-review harness for WinUI UI changes.
