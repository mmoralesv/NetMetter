# NetMetter

A tiny Windows tray tool that shows each connected network interface and its live
upload / download speed right on the taskbar.

```
Ethernet        Wi-Fi
↑  63.3 KB/s    ↑  0.0 KB/s
↓   7.0 KB/s    ↓  1.2 KB/s
```

## Features

- One column per connected interface (upload ↑ and download ↓), updated every second.
- Sits on the taskbar next to the notification area and avoids covering taskbar buttons
  (e.g. the Windows 11 Widgets button). Drag it anywhere along the taskbar and the spot is remembered.
- Hover a column for details: adapter description, link speed, IP addresses, totals sent/received.
- Follows the light/dark taskbar theme, scales with DPI, supports top/bottom/left/right taskbars.
- Hides automatically while a full-screen app is running or an auto-hide taskbar is tucked away.
- Right-click the widget or tray icon for options:
  - **Interfaces**: choose which adapters to show (by default, those with a default gateway,
    so virtual adapters like Hyper-V / VirtualBox host-only are hidden).
  - **Show interface names**: turn off for a compact two-line layout.
  - **Units**: bytes (KB/s, MB/s) or bits (Kbps, Mbps).
  - **Update interval**, **Reset position**, **Start with Windows**, **Network settings…**, **Exit**.

Settings are stored in `%APPDATA%\NetMetter\settings.json`.

## Build & run

Requires the .NET 10 SDK.

```bash
dotnet run -c Release
```

Publish a single small exe (needs the .NET 10 Desktop Runtime on the target machine):

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Add `--self-contained true` to bundle the runtime instead (larger exe, no prerequisites).

## How it works

Windows 11 removed taskbar toolbars ("desk bands"), so NetMetter uses a borderless, top-most,
per-pixel-alpha layered window positioned over the taskbar. It re-asserts its z-order whenever the
foreground window changes, since clicking the taskbar would otherwise raise the taskbar above it.
Free space on the taskbar is found with UI Automation.

| File | Purpose |
| --- | --- |
| `src/NetworkMonitor.cs` | Samples per-interface byte counters and computes speeds |
| `src/TaskbarWidget.cs` | The layered window: layout, drawing, dragging, tooltips |
| `src/TaskbarInfo.cs` | Taskbar position/edge/DPI, auto-hide and full-screen detection |
| `src/TaskbarObstacles.cs` | Locates taskbar buttons via UI Automation for automatic placement |
| `src/TrayApplication.cs` | Tray icon, context menu, sampling loop |
| `src/AppSettings.cs` | JSON settings and the "Start with Windows" registry entry |

## Limitations

- Shows on the primary monitor's taskbar only.
- Speeds are measured at the IP layer per adapter, so traffic on a VPN adapter is also counted
  on the physical adapter that carries it.
