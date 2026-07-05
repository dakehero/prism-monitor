# Prism Monitor

Prism Monitor is a Windows ARM64 tray app for spotting compatibility-mode desktop processes, such as x64 or x86 apps running through Windows emulation.

## Features

- WinUI 3 tray application
- Native AOT `win-arm64` publish
- Lists compatibility-mode processes with cumulative CPU time
- Tooltip shows Top 5 compatibility-mode processes by name and architecture
- Main window shows process icons and supports ending or ignoring a process
- Ignore rules are persisted locally and apply to the main window, tooltip, and Toast notifications
- Toast notification for newly detected compatibility-mode processes, with quick actions to end or ignore
- Uses public Windows APIs and avoids CPU sampling

## Build

```powershell
dotnet test PrismMonitor.slnx
dotnet publish src/PrismMonitor.App/PrismMonitor.App.csproj -c Release -r win-arm64 --self-contained true -p:PublishAot=true
```

The published executable is written to:

```text
src/PrismMonitor.App/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/PrismMonitor.App.exe
```

## Usage

Run Prism Monitor from Start or another interactive Windows shell. The app starts in the notification area with standard user permissions so Windows Toast notifications can work. When full visibility into processes owned by other users or the system is needed, open Settings and choose `Restart as administrator`.

- Left-click the tray icon to show the process list.
- Right-click the tray icon to open the tray menu, then choose `Exit`.
- Use the main window to end a process or add/remove ignored process names.

## MSIX package

Release builds also publish a signed MSIX package for Windows ARM64. The current development package uses a self-signed certificate, so use the generated `Install.ps1` script from the release zip to trust the certificate and install the package.

After installation, launch Prism Monitor from Start or another interactive Windows shell. The app runs without elevation by default; use the in-app administrator restart only when elevated process visibility is needed.

The v0.5.x releases cover the full roadmap through process icons, termination, no-flicker refresh, ignore rules, and Toast notifications.
