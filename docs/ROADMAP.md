# Prism Monitor Roadmap

Prism Monitor is a Windows ARM64 tray app for observing compatibility-mode desktop processes. Development is now evidence-driven maintenance: fix confirmed defects, compatibility problems, and measured power regressions; add features when explicitly requested.

## Release Status

- Latest GitHub release: [v0.7.1](https://github.com/dakehero/prism-monitor/releases/tag/v0.7.1).
- v0.7 feature scope is closed with the monitoring pipeline and app identity rules. The Rules workflow is implemented but awaits mainline integration and package-level verification.
- Next release candidate: **v0.8.0**, not yet published. It combines the remaining Rules workflow with opt-in sign-in startup.
- Historical specifications and verification records describe their original builds, not the current installed or released version.

## Product Constraints

- Use Windows-provided process data and cumulative CPU time; do not add CPU sampling loops.
- Keep one monitoring pipeline for Processes, History, tray status, and notifications.
- Cache expensive identity enrichment and tolerate inaccessible processes.
- Keep tray interactions asynchronous and the main window reusable.
- Store rules and history locally; preserve migration and normal update behavior.
- Require real render review for UI changes and runtime evidence for power claims.

## Completed Baseline

| Series | Delivered scope |
| --- | --- |
| v0.3 | Process icons, termination, stable list updates, architecture labels |
| v0.4 | Persistent ignore rules |
| v0.5 | Notifications for newly observed compatible processes |
| v0.6 | Durable launch history and list/detail UI |
| v0.6.1 | Notification navigation to current processes or history |
| v0.7.0-v0.7.1 | Shared monitoring host, refresh coalescing, enrichment cache, partial metadata, power-aware cadence |

The released monitoring policy uses 3-second refreshes on AC or unknown power, 10 seconds on battery with the main window visible, and a 30-second fallback on battery while hidden. Interactions and configuration or power changes request refreshes through the same coordinator.

Evidence: [monitoring and battery verification](verification/2026-07-10-v0.7.0-v0.7.1-monitoring-verification.md). These measurements describe that candidate and are not a new benchmark of v0.8.

## v0.7 Rules Closeout

Implemented, pending integration and release:

- App identity rules match package, path, publisher, or process name, with optional architecture qualification.
- Suppression targets include Processes, History, Tray, Toast, and All.
- Processes and History offer an `Ignore app` action.
- Rules is a dedicated view with creation, target editing, and removal.
- Legacy ignored names migrate without losing user choices.
- Rule rows reconcile in place, including target collisions, to avoid flicker.
- UI changes notify the shared monitoring host to reload configuration.

Evidence: [Rules UI review](verification/2026-07-10-v0.7.2-live-ui-review.md). The historical v0.7.2 label identifies development work; no v0.7.2 release has been published.

The earlier v0.7.3 diagnostics and v0.7.4 polish proposals are no longer scheduled milestones. Completed UI fixes are part of this closeout; additional diagnostics and polish require a concrete need.

## v0.8.0 Sign-In Startup

Status: implemented release candidate; not released.

- Add an opt-in Settings switch using Windows `StartupTask`.
- Default to disabled and start in the tray after sign-in.
- Let Windows persist and manage the startup state.
- Respect user-disabled and policy-controlled states.
- Keep the existing standard-user execution model.

### Remaining Release Checks

- [ ] Merge the reviewed candidate into main.
- [ ] Verify a signed package update preserves existing rules and history.
- [ ] Render-review the integrated Processes, History, Rules, and Settings views.
- [ ] Verify rule changes across tray menu/tooltip and Toast notifications.
- [ ] Verify Toast navigation for running and exited processes.
- [ ] Enable and disable startup, restart the app, and confirm state readback.
- [ ] Verify Windows Startup Apps changes are reflected in Settings.
- [ ] Verify a real sign-in starts one tray instance without opening the main window.
- [ ] Publish a version-matched package, tag, and release notes after approval.

Compilation, Native AOT publishing, and unsigned MSIX validation do not replace these installed-app checks. See [closeout verification](verification/2026-09-14-release-closeout.md) for the current evidence and blockers.

## Unscheduled Candidates

These are ideas, not commitments for v0.8:

- Notification policy presets.
- Local diagnostics for refresh cadence, cache state, and power mode.
- Packaging and Store submission improvements.
- Localization after the English baseline stabilizes.
- A CLI render-review harness for WinUI.

## Maintenance Triggers

Start an iteration for a reproducible defect, a Windows compatibility change, a measured idle-power regression, a broken build/package path, or an explicit feature request. Each iteration should produce a scoped fix, relevant verification, and a reviewable PR. Without an actionable trigger, leave the code unchanged.
