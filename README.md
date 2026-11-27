# NetMetter

A tiny Windows tray tool that shows each connected network interface and its live
upload / download speed, either on the taskbar or in a floating window.

[![Get it from the Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9PHLZGZZN7CB)

```
Ethernet        Wi-Fi
↑  63.3 KB/s    ↑  0.0 KB/s
↓   7.0 KB/s    ↓  1.2 KB/s
```

## Features

- One column per connected interface (upload ↑ and download ↓), updated every second.
- **Two display modes**, chosen on first run and switchable from the menu:
  - **Taskbar** — a readout drawn on top of the taskbar, next to the notification area.
    It leaves room for the Windows 11 Widgets button. Drag it anywhere along the taskbar.
  - **Floating** — a small always-on-top panel you can place anywhere.
- **Any monitor**: pick which display the meter lives on, and it attaches to that monitor's
  taskbar. Unplug that monitor and it falls back to the primary one instead of disappearing.
- Hover a column for details: adapter description, link speed, IP addresses, totals sent/received.
- Follows the light/dark theme, scales with DPI, supports top/bottom/left/right taskbars.
- Hides automatically while a full-screen app is running or an auto-hide taskbar is tucked away.
- Stays off the clock and the Widgets button, and notices when another app covers it.
- Right-click the meter or tray icon for options:
  - **Interfaces**: choose which adapters to show (by default, those with a default gateway,
    so virtual adapters like Hyper-V / VirtualBox host-only are hidden).
  - **Show meter**: taskbar or floating window.
  - **Monitor**: which display to show it on (monitors without a taskbar are listed too, and
    selectable in floating mode).
  - **Show interface names**, **Units** (bytes/bits), **Update interval**,
    **Reset position**, **Start with Windows**, **About**, **Exit**.

NetMetter reads network information locally and sends nothing over the network
(see [docs/PRIVACY.md](docs/PRIVACY.md)). Settings live in `%APPDATA%\NetMetter\`.

## Build & run

Requires the .NET 10 SDK.

```bash
dotnet run --project src
dotnet test        # 56 unit tests (uses NetMetter.sln)
```

## Package for the Microsoft Store

NetMetter ships as an MSIX bundle. The Store signs and hosts it — no certificate needed.

```powershell
./build/Package.ps1
```

See [packaging/README.md](packaging/README.md) for identity values, sideload testing,
and the Windows App Certification Kit. The end-to-end publishing and CI/CD plan is in
[docs/STORE.md](docs/STORE.md).

## How it works

Windows 11 removed taskbar toolbars ("desk bands"), so in taskbar mode NetMetter uses a
borderless, top-most, per-pixel-alpha layered window positioned over the taskbar. It
re-asserts its z-order whenever the foreground window changes, since clicking the taskbar
would otherwise raise the taskbar above it. Free space for the Widgets button is worked out
from documented taskbar layout preferences — no accessibility or undocumented APIs are used,
which keeps it within Store policy.

Each monitor gets its own taskbar window while "Show my taskbar on all displays" is on, so
the meter enumerates them and attaches to the one for the chosen display, remembered by its
`\\.\DISPLAYn` device name rather than by index. The notification area and the Widgets strip
are treated as keep-out spans, so neither a drag nor a position saved on a wider taskbar can
park the meter on top of the clock.

Z-order alone does not guarantee the meter is actually on screen: `SetWindowPos` with
`HWND_TOPMOST` does not reliably reorder windows that are already top-most, so another
always-on-top app can sit in front of it. Once every five seconds the meter walks the windows
in front of itself, skipping hidden, cloaked and near-transparent ones, and skipping the shell
(the taskbar coming forward is normal, and Windows keeps a full-screen input-occlusion frame in
front of the whole desktop — which is also why asking `WindowFromPoint` who owns a pixel does
not work here). Anything still in the way three checks later is named in the log and in a
one-off notification, so a hidden meter does not look like a broken one.

| File | Purpose |
| --- | --- |
| `src/NetworkMonitor.cs` | Samples per-interface byte counters and computes speeds |
| `src/MeterWindow.cs` | The layered window: taskbar and floating layout, drawing, dragging, tooltips |
| `src/TaskbarInfo.cs` | Enumerates the taskbars (one per monitor): position, edge, DPI, Widgets-button reservation, auto-hide and full-screen detection |
| `src/Placement.cs` | Where the meter sits along a taskbar, and the keep-out spans it avoids |
| `src/Occlusion.cs` | Detects another app drawing over the meter |
| `src/TrayApplication.cs` | Tray icon, context menu, sampling loop |
| `src/WelcomeDialog.cs` | First-run mode + startup consent |
| `src/StartupRegistration.cs` | "Start with Windows" via MSIX StartupTask (packaged) or the Run key (plain exe) |
| `src/AppSettings.cs` | JSON settings |
| `src/AppEnvironment.cs`, `src/AppLog.cs` | Packaged/unpackaged detection, diagnostic log |
| `tools/AssetGen/` | Generates the `.ico` and MSIX logos from `src/AppIcon.cs` |
| `tests/NetMetter.Tests/` | xUnit tests: speed formatting, rate math, settings, placement, monitor selection |
| `build/Package.ps1` | Builds the MSIX bundle |
| `.github/workflows/` | CI (build/format/test/pack) and Store release/rollout/listing pipelines |

## Limitations

- Taskbar mode needs a taskbar on the chosen monitor, so it requires "Show my taskbar on all
  displays" for secondary monitors; floating mode works on any monitor regardless.
- A secondary monitor's taskbar has no notification-area window to measure, so on those the
  meter leaves a fixed clock-sized gap at the far end rather than measuring the real clock.
- Speeds are measured at the IP layer per adapter, so traffic on a VPN adapter is also counted
  on the physical adapter that carries it.
