# Prism Monitor

Prism Monitor is a Windows ARM64 tray app for spotting compatibility-mode desktop processes, such as x64 or x86 apps running through Windows emulation.

## Features

- WinUI 3 tray application
- Native AOT `win-arm64` publish
- Lists compatibility-mode processes with cumulative CPU time
- Tray tooltip summarizes compatible processes; the right-click menu shows top processes
- Main window shows process icons and supports ending or ignoring a process
- App identity rules persist locally, with separate targets for Processes, History, Tray, and Toast
- Launch history retains observed starts for 30 days
- Toast notification for newly detected compatibility-mode processes, with quick actions to end or ignore
- Optional Windows sign-in startup, controlled from Settings and managed by Windows Startup Apps
- Uses public Windows APIs and avoids CPU sampling

This README describes the development branch. The latest GitHub release is [v0.7.1](https://github.com/dakehero/prism-monitor/releases/tag/v0.7.1); Rules workflow improvements and sign-in startup are pending in the **v0.8.0** candidate. See the [roadmap and remaining release checks](docs/ROADMAP.md).

## Install

<p>
  <a href="https://apps.microsoft.com/detail/9NHJX7QKXW8F?hl=neutral&gl=SG&ocid=pdpshare">
    <img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Get it from Microsoft" width="220" />
  </a>
</p>

The Store package is the recommended installation path for normal use.

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

Run Prism Monitor from Start or another interactive Windows shell. The app starts in the notification area with standard user permissions so Windows Toast notifications can work. Some processes owned by other users or the system may be hidden by Windows permissions.

- Left-click the tray icon to show the process list.
- Right-click the tray icon to open the tray menu, then choose `Exit`.
- Use Processes or History to ignore an app, and Rules to edit suppression targets or remove rules.
- Use Settings to choose whether Prism Monitor starts in the tray after you sign in.

## Development MSIX package

The packaging workflow produces a signed MSIX for Windows ARM64. A local build or `dotnet publish` alone does not publish a GitHub release or submit to the Store. Development packages use a self-signed certificate; use the generated `Install.ps1` from a test release zip.

After installation, launch Prism Monitor from Start or another interactive Windows shell. The app runs without elevation by default.

The v0.7 baseline adds shared monitoring and cached process enrichment. On battery, refresh slows to 10 seconds while the main window is visible and 30 seconds while hidden, with interaction-triggered refreshes.

## License

Prism Monitor is licensed under the [MIT License](LICENSE).
