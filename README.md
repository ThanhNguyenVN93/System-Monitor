# System Monitor

A lightweight, always-on-top HUD overlay for Windows that displays real-time system stats with an acrylic blur background.

![screenshot](docs/screenshot.png)

## Features

- **CPU** — load %, temperature (°C), clock speed (GHz), power draw (W)
- **GPU** — load %, temperature (°C), hotspot temp, power draw (W)
- **RAM** — used / total (GB)
- **VRAM** — used / total (GB) with progress bar
- **Uptime** — live HH:MM:SS counter
- Acrylic blur background (Windows 10/11)
- Snap-to-edge drag
- Click-through toggle
- Multi-monitor support
- Accent color picker
- Auto-start via Scheduled Task (no UAC prompt on login)
- Update check against GitHub Releases (asks before installing, SHA-256 verified)
- **Report Issue** in the tray menu: copies diagnostics to the clipboard and opens the support page

## Requirements

- Windows 10 or 11 (x64)
- .NET Framework 4.8.1
- **Run as Administrator** (required for hardware sensor access; the manifest requests it)

## How CPU sensors work

```
System Monitor.exe (admin)
├── LibreHardwareMonitor + PawnIO driver → CPU temp / power (MSR, RAPL), GPU, RAM, storage
├── Super I/O fallback                   → "CPU Core" temperature from the motherboard chip
├── PerformanceCounter                   → CPU clock
└── Intel Power Gadget (optional)        → extra CPU package power, only if you add its files
```

The app does not guess from Windows settings. It watches whether CPU temperature/power can
actually be read; if neither can for ~10 seconds it works out why:

- **Not running as Administrator** → tooltip on the CPU line, no security prompt.
- **Secure Boot** on → balloon tip explaining it.
- **VBS / Memory Integrity running** (detected via WMI `Win32_DeviceGuard`, not the registry) →
  asks whether to disable VBS / HVCI / Hyper-V. Choosing *No* is remembered; use the tray item
  **Check CPU Sensor Access...** to revisit. Disabling these lowers Windows protection and stops
  WSL2, Docker Desktop and Windows Sandbox.

If sensors work, none of this is shown, even when VBS is on.

## Build

```
msbuild frm_sys_monitor.csproj /p:Configuration=Release
```

Output: `bin\Release\System Monitor.exe` — a single self-contained executable; libraries and the
PawnIO driver are embedded and extracted on first run.

### Optional: Intel Power Gadget

`EnergyLib64.dll`, `EnergyDriver.sys` and `EnergyDriver.inf` (plus the VC++ runtime DLLs they need)
belong to Intel Power Gadget and are **not** included in this repository. If you own a copy, put them
in `lib\` and rebuild; the project embeds them automatically. Without them the app simply uses
LibreHardwareMonitor's readings.

## Updates

Release assets are `SystemMonitor.exe` and `SystemMonitor.exe.sha256`. The app checks the latest
release shortly after start (toggle: tray → *Check for updates on startup*), shows a tray notification
and only downloads after you confirm. The download is verified against the published SHA-256, then the
exe is swapped and the app restarts.

## Third-party Libraries

| Library | License | How bundled |
|---------|---------|-------------|
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 | Embedded resource |
| [PawnIO](https://pawnio.eu/) | see project | Embedded driver, installed as a service when needed |
| [Guna UI2](https://gunaui.com/) | Commercial | Embedded resource |

## License

MIT — see [LICENSE](LICENSE)
