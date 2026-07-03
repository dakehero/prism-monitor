# Native Guard

Native Guard is a Windows ARM64 tray app for spotting non-native desktop processes, such as x64 or x86 apps running through Windows emulation.

## Features

- WinUI 3 tray application
- Native AOT `win-arm64` publish
- Lists non-native processes with cumulative CPU time
- Tooltip shows Top 5 non-native processes by cumulative CPU time
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

Run `NativeGuard.App.exe` as administrator. The app starts in the notification area.

- Left-click the tray icon to show the process list.
- Right-click the tray icon to exit.
