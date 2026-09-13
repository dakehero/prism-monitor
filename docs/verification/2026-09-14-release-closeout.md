# Release Closeout Verification

Date: 2026-09-14

## Integrated Candidate

The development branch now includes main commit `223e492` (the released v0.7.1 monitoring pipeline), the Rules workflow, and sign-in startup. Package identity remains unchanged; the candidate package version is `0.8.0.0`.

Merge resolution preserves:

- The shared MonitoringHost and cached enrichment, with no MainWindow process timer.
- Rule changes notifying the host through ConfigurationChanged.
- Stable rule row reconciliation and app identity metadata on process rows.
- Cached icon keys and limited-details presentation from main.
- StartupTask injection and the opt-in Settings switch.

## Checks

- `dotnet test PrismMonitor.slnx -c Release --no-restore`: 191 passed.
- ARM64 self-contained Native AOT publish: passed.
- Unsigned MSIX creation and MakeAppx validation: passed.
- Package inspection confirms version 0.8.0.0, PrismMonitorStartup disabled by default, and PrismMonitor.App.exe as the startup target and a present payload entry.
- Whitespace and merge-marker checks are performed before commit.

Publish command:

```powershell
dotnet publish src/PrismMonitor.App/PrismMonitor.App.csproj -c Release -r win-arm64 --self-contained true --no-restore -p:PublishAot=true -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:AppxBundle=Never -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxPackageDir=artifacts/msix-closeout/
```

Tests reported NU1900 from an unavailable NuGet vulnerability feed. Packaging reported missing mspdbcmf.exe, so no symbols package was generated. Neither check is described as warning-free.

## Remaining Runtime Review

No new screenshot or installed-app pass was completed for this integrated candidate. The current Computer Use tool exposes no native app control. An earlier debug launch was rejected because the installed non-development v0.7.1 package uses the same identity. That attempted launch did not verify the candidate.

The unsigned artifact is a build-validation artifact, not a signed installer or a Store submission. The installed application was not replaced during this closeout.

The [roadmap](../ROADMAP.md#remaining-release-checks) retains signed-update data migration, integrated UI rendering, tray/Toast behavior, startup state readback, and real sign-in as open release checks. Historical UI and battery records remain evidence for their respective builds only.
