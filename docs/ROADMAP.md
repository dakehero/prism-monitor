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

## v0.6 Notification Controls

Status: planned.

Make notifications configurable instead of treating every notification as an always-on behavior.

Acceptance criteria:

- Users can enable or disable Toast notifications from the main window.
- Users can choose whether notifications include quick actions.
- Users can configure quiet hours for notification suppression.
- The app records a lightweight in-memory notification history for the current session.
- Repeated notifications for the same process name are rate-limited.
- Notification settings persist across app restarts.

## v0.7 Low-Privilege Process Pipeline

Status: planned.

Reduce background handle usage while improving standard-user visibility. The app should stop treating process handle access as the first step of every refresh. Instead, split process discovery into a lightweight snapshot layer and a best-effort enrichment layer.

### Snapshot Provider

Use low-privilege system snapshots to build the base process list.

Acceptance criteria:

- The snapshot layer can list PID, process name, and cumulative CPU time without requiring administrator elevation.
- System-owned processes remain visible when Windows denies limited-information process handles.
- The snapshot layer does not read executable paths, icons, or architecture.
- Snapshot refreshes are safe to run in background notification and tray status flows.

### Enrichment Provider

Use handle-based and file-based APIs only to enrich processes that need more detail.

Acceptance criteria:

- Path, architecture, icon, and termination capability are populated only when the process can be opened with least-privilege access.
- Architecture detection prefers process APIs for runtime machine values.
- x64-compatible processes with readable executable paths are additionally checked for ARM64EC / ARM64X PE metadata.
- Processes whose architecture cannot be verified are shown as `Unknown` or `Unavailable` instead of being guessed.
- Termination actions are disabled for inaccessible or protected processes.

### Enrichment Cache

Avoid repeating expensive or permission-sensitive work on every refresh.

Acceptance criteria:

- PID-scoped metadata such as architecture, executable path, and icon lookup results are cached while the process remains alive.
- The cache is invalidated when a PID exits or its creation identity changes.
- Background notification scans do not re-open every process handle every cycle.
- Main-window refreshes may request richer enrichment, but they still reuse cached metadata where possible.

### Power and Store Constraints

Keep the design friendly to Microsoft Store certification and laptop battery life.

Acceptance criteria:

- The app continues to run without `allowElevation`.
- Battery-mode background behavior remains interaction-driven unless the main window is visible.
- Toast detection never requires administrator elevation.
- The implementation avoids CPU sampling loops and uses system-provided process data.

## Future Ideas

- Publisher-aware or path-aware ignore rules.
- Store-ready packaging with a Microsoft Partner Center certificate.
- Localized UI resources after the English baseline is stable.
