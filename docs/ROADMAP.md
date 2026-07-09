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

## v0.7 Low-Power App Identity Roadmap

Status: planned.

v0.7 is the release that makes Prism Monitor feel cheap enough to leave running all day. The theme is "observe lightly, enrich deliberately": avoid repeated full process scans in the tray path, cache expensive identity details, and make all filtering decisions flow through one app-identity rule engine.

### Goals

- Reduce hidden-to-tray and battery-mode work without losing newly started compatibility-mode process detection.
- Separate lightweight process discovery from expensive enrichment such as executable path, icon, architecture heuristics, package identity, and signing identity.
- Replace name-only ignores with app identity rules that work consistently across Processes, History, tray tooltip/menu, and Toast notifications.
- Keep the UI modern and low-friction: simple default actions, richer details only when the user expands or edits.

### Non-goals

- Do not add CPU sampling loops or ETW-based CPU profilers.
- Do not require administrator elevation for the normal Store-facing app.
- Do not build a full task-manager replacement.
- Do not redesign packaging, signing, or release automation as part of 0.7.

### v0.7.0 Low-Power Process Pipeline

Build a shared monitoring pipeline that all runtime surfaces use instead of each surface enumerating processes independently.

Deliverables:

- Introduce a lightweight snapshot model containing PID, process name, cumulative CPU time when available, and detection time.
- Add a single background coordinator for tray tooltip, tray menu, Toast detection, and history recording.
- Move main-window refreshes onto the same snapshot feed, with explicit requests for richer details when the window is visible.
- Make battery behavior adaptive:
  - Hidden to tray on battery: use event-like or interaction-triggered refresh where available, with a conservative fallback interval.
  - Main window visible on battery: refresh at a lower cadence and never block UI rendering on enrichment.
  - Plugged in: keep responsive monitoring, but still avoid duplicate scans.

Acceptance criteria:

- Hidden-to-tray monitoring no longer opens every process handle once per second.
- Tray, Toast, history, and main window do not each perform their own full process enumeration loop.
- Starting a new x86, x64, or ARM64EC compatibility-mode app still appears in the next expected monitoring cycle.
- The app continues to use Windows/system process data and avoids CPU sampling loops.
- Tests cover coordinator fan-out, battery cadence selection, and no-duplicate-refresh behavior.
- Manual power smoke test records CPU delta, wake behavior, working set, and refresh cadence on battery while hidden to tray.

### v0.7.1 Enrichment Cache

Add a cache for expensive or permission-sensitive metadata. The snapshot path should work with partial identity, and enrichment should add richer details opportunistically.

Deliverables:

- Add a PID-scoped process identity cache keyed by PID plus creation identity when available.
- Cache architecture, executable path, icon identity/cache key, package identity, publisher/signing identity, ARM64EC / ARM64X classification, and last enrichment result.
- Treat inaccessible metadata as a cacheable limited-detail state with a retry policy, not as a crash or permanent disappearance.
- Keep icon extraction off the hot refresh path and reuse existing icons whenever the executable identity has not changed.

Acceptance criteria:

- Repeated refreshes do not re-read path, icon, PE metadata, or signing/package identity for unchanged processes.
- PID reuse invalidates stale metadata.
- Protected or unreadable processes remain visible with limited identity.
- ARM64EC / ARM64X classification is preserved and tested.
- Tests cover cache hits, cache invalidation, inaccessible metadata fallback, and targeted ARM64EC enrichment.

### v0.7.2 App Identity Rules

Replace the current ignored-name list with a versioned rule model. Rules describe what app identity they match and which Prism Monitor surfaces they suppress.

Deliverables:

- Add an `AppIdentityRule` model with match fields for process name, executable path, package identity, publisher/signing identity, and optional architecture.
- Add suppression targets for Processes, History, tray tooltip/menu, Toast notifications, and All.
- Add a single rule evaluator used by every surface.
- Migrate existing ignored process names into all-surface process-name rules on first run.
- Keep the legacy ignore data readable for one release cycle so users do not lose choices during upgrade/downgrade testing.

Acceptance criteria:

- Matching is case-insensitive for Windows process names and paths.
- Rules behave predictably when only partial metadata is available.
- Processes, History, tray tooltip/menu, and Toast notifications all use the same evaluator.
- Existing ignored apps keep their behavior after upgrade.
- Tests cover rule matching, target-specific suppression, migration, legacy-read fallback, and partial-metadata fallback.

### v0.7.3 Rules UI and Workflow

Turn Filters into a rule-management experience while keeping the common "ignore this app" action one-click simple.

Deliverables:

- Rename the Filters view to Rules if the UI copy reads better after implementation review.
- Let users create a rule from Processes, History, or the Rules view using the best available identity without retyping.
- Keep the default action simple: "Ignore app" creates an all-surface rule using the strongest available identity.
- Add an advanced edit surface for match fields and suppression targets.
- Preserve the current no-flicker list behavior and use real WinUI render review before handoff.

Acceptance criteria:

- Rules list updates in place and does not visibly flicker.
- Process and History rows expose rule actions without cluttering the compact card layout.
- Advanced rule editing is discoverable but not the default path.
- Deleting or changing a rule gives clear feedback.
- UI render review covers Processes, History, Rules, Settings, tray menu, and Toast activation flows affected by rules.

### Cross-cutting Design

The 0.7 data flow should converge on this shape:

```text
Windows process data
  -> lightweight snapshots
  -> enrichment cache
  -> app identity rules
  -> Processes / History / Tray / Toast
```

The lightweight snapshot layer should not know about UI. The enrichment cache should not make suppression decisions. The rule evaluator should be pure enough to test in `PrismMonitor.Core`. WinUI, tray, Toast, and icon extraction remain in `PrismMonitor.App`.

### Release Gates

v0.7 is not complete until both power behavior and rule consistency are proven.

- Core tests pass for snapshot coordination, enrichment cache, rule migration, rule matching, and partial metadata.
- App build passes for `win-arm64` Release.
- Manual battery smoke test compares v0.6.1 and v0.7 hidden-to-tray behavior using the same machine state.
- Manual render review is done for every visible UI change, using XAML Live Preview when available or a launched local build screenshot.
- No generated MSIX artifacts, package output, local logs, or certificates are committed.

### 0.8 Candidates

These ideas are intentionally deferred unless 0.7 lands early:

- More advanced notification policy presets.
- A dedicated diagnostics page for refresh cadence, cache state, and power mode.
- Store-ready packaging/signing improvements.
- Localization resources after the English baseline stabilizes.

## Future Ideas

- Command-line render-review harness for WinUI UI changes.
