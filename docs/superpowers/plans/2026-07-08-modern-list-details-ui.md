# Compact List/Details UI Plan

## Goal

Modernize the Processes and History pages so they feel less like fixed-width tables and more like touch-friendly WinUI lists.

## Scope

- Replace Processes and History pseudo-table rows with compact expandable rows.
- Default rows show the app icon, name, architecture, and the most important summary metric.
- Expanded rows show PID, CPU time or history timing, full executable path, copy actions, and process actions where applicable.
- Keep the existing `NavigationView`, filtering, ignore rules, history storage, and in-place list updates.
- Preserve power-aware process detection; this task does not add sampling or new background loops.

## Implementation Notes

- Processes use `ProcessRow.ExecutablePath`, summary text, and existing icons.
- History uses `HistoryRow.Icon`, `LastProcessId`, summary text, and readable wrapped path details.
- History summary files written before `LastProcessId` existed are rebuilt from JSONL when possible, so upgraded installs can show the last PID.
- PID and path values are copyable through row-level copy buttons.
- Layout tests reject `ListView.Header` pseudo-table headers and require expandable compact detail rows.

## Verification Checklist

- [x] Targeted history and layout regression tests pass.
- [x] Full solution tests pass.
- [x] ARM64 Release app build passes.
- [x] Local MSIX install/render review is performed for the visible UI change.
- [x] Commit and push the branch.
