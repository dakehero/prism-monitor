# Repository Agent Notes

Prism Monitor is a Windows ARM64 WinUI 3 tray app for observing desktop processes running in Windows compatibility mode. Keep repository-facing docs in English unless the user asks otherwise; chat with the user in Chinese by default.

## Product Constraints

- Use Windows/system process data only. Do not add CPU sampling loops.
- Keep background monitoring power-aware, especially while hidden to tray or on battery.
- Use neutral wording such as "compatibility-mode" or "emulated".
- Keep tray UI lightweight: summary tooltip, dynamic right-click menu, and fast left-click access to the main window.
- The main window is the place for details, history, filters, settings, and destructive actions.
- Closing the main window hides it to tray; right-click tray opens a menu; Exit belongs in that menu.

## Implementation Notes

- `src/PrismMonitor.Core` should hold testable domain behavior: process models, filtering, history, and settings logic.
- `src/PrismMonitor.App` should hold WinUI, tray, notifications, process interop, icons, packaging, and app lifetime.
- Prefer .NET / WinRT / Windows App SDK APIs when practical. Use narrow Win32 interop when Windows has no suitable managed or WinRT API.
- Preserve ARM64EC/ARM64X handling. `IsWow64Process2` alone is not enough for every architecture case already discussed in this project.
- Persist ignore rules and history in app-local data so normal updates do not forget user choices.

## UI Review Rule

Every visible UI change needs a real render review before handoff.

- For ordinary MainWindow/XAML changes, use Visual Studio XAML Live Preview with XAML Hot Reload as the default review path when available.
- If XAML Live Preview is unavailable in the current environment, launch a local Debug/Release build and capture the affected window instead.
- Use MSIX install/package review only for package-identity behavior: tray startup, Toast activation, app icons, manifest, permissions, Start menu, signing, or installer flow.
- Capture and inspect the affected screen. Check alignment, spacing, clipping, scrollbars, text fit, empty states, and window size.
- Mention the render-review method in the final handoff. If it cannot be done, state exactly what remains unverified.

Do not treat XAML diffs, string tests, or successful compilation as enough for visible UI work.

## Local Verification

Use the smallest relevant check first, then broaden when the change touches shared behavior.

```powershell
dotnet test tests/PrismMonitor.Core.Tests/PrismMonitor.Core.Tests.csproj --configuration Release --no-restore
dotnet test PrismMonitor.slnx --configuration Release --no-restore
dotnet build src/PrismMonitor.App/PrismMonitor.App.csproj --configuration Release --runtime win-arm64 --self-contained true --no-restore
```

For local MSIX smoke tests, use the generated package plus:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-local-msix.ps1 -PackageDir "<package-dir>" -Launch
```

Known context: local packages are self-signed; generated MSIX artifacts should not be committed; existing Windows SDK trim warnings are expected unless a change specifically removes them.

## Git Handoff

- The user expects completed tasks to be committed and pushed.
- Keep commits scoped. Do not include local package artifacts.
- Final handoff should briefly say what changed, what was verified, whether UI render review was needed/performed, and the commit hash.
