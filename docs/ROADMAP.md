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

## Future Ideas

- Publisher-aware or path-aware ignore rules.
- Store-ready packaging with a Microsoft Partner Center certificate.
- Localized UI resources after the English baseline is stable.
