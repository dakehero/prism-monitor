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

## v0.7 Low-Power App Identity Pipeline

Status: planned.

Make Prism Monitor cheaper to run on battery and more precise about what an app is. v0.7 combines the old low-privilege process-pipeline plan with the newer app-identity rules plan: first stop doing repeated full handle scans, then build richer rules on top of cached process identity.

### v0.7.0 Low-Power Process Pipeline

Split process discovery into a lightweight snapshot layer and a best-effort enrichment layer. Tray, Toast, and history flows should not treat `OpenProcess` as the first step of every refresh.

Acceptance criteria:

- Background monitoring does not open every process handle once per second.
- The first pass collects PID, process name, and cumulative CPU time through low-privilege system data.
- Background tray and Toast flows use lightweight snapshots plus cached enrichment.
- Main-window refreshes may request richer details, but they reuse cached metadata whenever possible.
- Battery-mode behavior is interaction-driven when hidden to tray, and visibly lower-frequency when the main window is open.
- The app continues to avoid CPU sampling loops.

### v0.7.1 Enrichment Cache

Cache expensive or permission-sensitive metadata so repeated refreshes do not re-read the same path, icon, architecture, or ARM64EC information.

Acceptance criteria:

- PID-scoped metadata includes architecture, executable path, app icon, package identity when available, publisher/signing identity when available, and ARM64EC / ARM64X classification.
- The cache is invalidated when a PID exits or its creation identity changes.
- Processes with unreadable paths or metadata degrade to process-name identity without crashing.
- Architecture detection prefers runtime process APIs and uses PE metadata only as a targeted enrichment step.
- Inaccessible or protected processes remain visible with limited detail instead of disappearing from every surface.

### v0.7.2 App Identity Rules

Replace the simple ignored-name list with rules that describe app identity and what Prism Monitor should suppress.

Acceptance criteria:

- Rules can match by process name, executable path, package identity, publisher/signing identity, or a combination of available fields.
- Rule matching is case-insensitive for Windows process names and paths.
- Each rule can suppress main-window visibility, tray tooltip/menu visibility, Toast notifications, history surfacing, or all compatibility-mode surfacing.
- The main window, tray tooltip, tray menu, Toast notifications, and history views all use the same rule evaluator.
- Existing name-only ignored apps migrate into equivalent v0.7 rules on first run.
- Rule evaluation remains safe when metadata is partial, missing, or unreadable.

### v0.7.3 Rules UI and Workflow

Turn the current Filters page into a rules management surface without making common actions harder.

Acceptance criteria:

- Users can create a rule from the Processes page, History page, or Filters page without retyping known process details.
- The default action stays simple: ignoring an app creates an all-surface rule using the best available identity.
- Advanced rule editing exposes match fields and suppression targets without overwhelming the default list view.
- The rule list updates without flicker and remains stable while background refreshes continue.
- Destructive actions such as deleting a rule show clear feedback.

### v0.7 Release Gates

v0.7 is not complete unless it proves the new pipeline is cheaper and the new rules are consistent.

Acceptance criteria:

- Add tests for snapshot/enrichment separation, cache invalidation, rule migration, rule matching, and partial-metadata fallback.
- Add a manual power smoke test comparing hidden-to-tray battery behavior before and after the pipeline change.
- Add a main-window render review for Processes, History, Filters/Rules, and Settings after the rules UI lands.
- No local MSIX artifacts or generated package output are committed.
- Store-facing behavior still works without administrator elevation.

## Future Ideas

- Store-ready packaging with a Microsoft Partner Center certificate.
- Localized UI resources after the English baseline is stable.
- Command-line render-review harness for WinUI UI changes.
