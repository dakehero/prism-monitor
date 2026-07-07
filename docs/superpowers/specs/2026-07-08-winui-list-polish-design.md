# WinUI List Polish Design

## Goal

Make the Processes and History pages feel like modern WinUI utility surfaces instead of rough expandable tables.

## Design Principles

- Keep the current `NavigationView`, `ListView`, and `Expander` structure.
- Use Fluent-style hierarchy: clear page title, subdued status text, compact command buttons, rounded list rows, and inline details.
- Keep default rows glanceable: icon, name, architecture, and one summary line.
- Keep secondary details hidden until expansion: PID, CPU time, seen timestamps, path, and process actions.
- Make copy behavior value-based: PID values are buttons that copy themselves; paths stay selectable instead of showing repeated copy buttons.
- Prefer system theme resources and built-in icon glyphs over custom color palettes or heavy decoration.
- Keep rows stable during refresh; do not reintroduce table headers, equal-width columns, or full-list flicker.

## Processes Page

- Header contains the page title, a live count/status line, and an icon-only refresh command.
- Each process row has a rounded card-like backplate with subtle border, hover-friendly spacing, and a restrained architecture badge.
- Expanded content is inline under the row, without a nested card.
- PID is directly clickable/copyable.
- Process actions are direct `End process` and `Ignore` buttons rather than a more menu.

## History Page

- Header contains the page title, a live count/status line, and an icon-only refresh command.
- Filters live in a compact toolbar under the header. The toolbar is visually related to the page but does not dominate the list.
- Each history row mirrors process row treatment: icon, name, architecture badge, launch summary, and inline expandable details.
- PID is directly clickable/copyable; path remains selectable text without a repeated copy button.

## Verification

- Add XAML regression tests requiring page status text, compact command affordances, rounded row backplates, inline details, value-based PID copy, and direct process actions.
- Build the app, install or launch a local render, and screenshot-review Processes and History default and expanded states.
