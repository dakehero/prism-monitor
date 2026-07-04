# Native Guard

Native Guard is a Windows ARM64 tray app for spotting non-native desktop processes, such as x64 or x86 apps running through Windows emulation.

## Features

- WinUI 3 tray application
- Native AOT `win-arm64` publish
- Lists non-native processes with cumulative CPU time
- Tooltip shows Top 5 non-native processes by name and architecture
- Main window shows process icons and supports ending or ignoring a process
- Ignore rules are persisted locally and apply to the main window, tooltip, and Toast notifications
- Toast notification for newly detected non-native processes, with quick actions to end or ignore
- Uses public Windows APIs and avoids CPU sampling

## Build

```powershell
dotnet test NativeGuard.slnx
dotnet publish src/NativeGuard.App/NativeGuard.App.csproj -c Release -r win-arm64 --self-contained true -p:PublishAot=true
```

The published executable is written to:

```text
src/NativeGuard.App/bin/Release/net10.0-windows10.0.26100.0/win-arm64/publish/NativeGuard.App.exe
```

## Usage

Run Native Guard as administrator. The app starts in the notification area and requests elevation at startup so it can see processes owned by other users and the system.

- Left-click the tray icon to show the process list.
- Right-click the tray icon to open the tray menu, then choose `退出`.
- Use the main window to end a process or add/remove ignored process names.

## MSIX package

Release builds also publish a signed MSIX package for Windows ARM64. The current development package uses a self-signed certificate, so use the generated `Install.ps1` script from the release zip to trust the certificate and install the package.

After installation, launch Native Guard from Start or another interactive Windows shell and accept the UAC prompt. Non-interactive commands such as `Start-Process "shell:AppsFolder\..."` cannot satisfy the elevation prompt and can fail with `0x800702E4`.

The v0.5.0 release is the first release intended to cover the full roadmap through process icons, termination, no-flicker refresh, ignore rules, and Toast notifications.
