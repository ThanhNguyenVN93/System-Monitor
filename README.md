# System Monitor

A lightweight, always-on-top HUD overlay for Windows that displays real-time system stats with an acrylic blur background.

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
- **VBS / Memory Integrity running** (detected via WMI `Win32_DeviceGuard`, not the registry) →
  asks whether to disable VBS / HVCI / Hyper-V. Choosing *No* is remembered; use the tray item
  **Check CPU Sensor Access...** to revisit. Disabling these lowers Windows protection and stops
  WSL2, Docker Desktop and Windows Sandbox.

If sensors work, none of this is shown, even when VBS is on.

## Build

```
msbuild frm_sys_monitor.csproj /p:Configuration=Release
```

No NuGet restore is needed: every dependency is checked in under `lib\` and referenced from there.

Output: `bin\Release\System Monitor.exe` — the only file in the folder. Libraries and the PawnIO
driver are embedded as resources (nothing is copied next to the exe, no `.config` file); at runtime
they are loaded from memory, or extracted into `%ProgramData%\SystemMonitor`, a folder only
Administrators can write to, when they must exist on disk.

Unit tests (pure logic: sensor selection, release parsing, folder ACL check):

```
dotnet test tests/frm_sys_monitor.Tests
```

### Optional: Intel Power Gadget

`EnergyLib64.dll`, `EnergyDriver.sys` and `EnergyDriver.inf` (plus the VC++ runtime DLLs they need)
belong to Intel Power Gadget and are **not** included in this repository. If you own a copy, put them
in `lib\` and rebuild; the project embeds them automatically. Without them the app simply uses
LibreHardwareMonitor's readings.

## Where to put the exe

The app runs as Administrator and auto-starts with highest privileges, so keep the exe in a folder
normal users can't modify, e.g. `C:\Program Files\System Monitor`. If it runs from somewhere like
Downloads, it shows a one-time warning. After moving it, toggle tray → *Start with Windows* off and on
so the scheduled task points to the new path.

Settings (`settings.cfg`) and the hardware error log (`hw_error.log`) are kept in
`%LocalAppData%\SystemMonitor`, so the exe folder holds only the exe. A `settings.cfg` left next to the
exe by version 1.2.0 or older is moved there on first start.

## Updates

Release assets are `SystemMonitor.exe` and `SystemMonitor.exe.sha256`. The app checks the latest
release shortly after start (toggle: tray → *Check for updates on startup*), shows a tray notification
and only downloads after you confirm. The download goes into the admin-only `%ProgramData%\SystemMonitor`
folder and is verified against the published SHA-256 there; then the exe is swapped and the app restarts.

## Releasing

1. Bump `AssemblyVersion` / `AssemblyFileVersion` in `Properties/AssemblyInfo.cs`.
2. Merge to `master`, then push a matching tag, e.g. `git tag v1.2.1 && git push origin v1.2.1`.

The **Release** workflow checks that the tag matches the version, builds, runs the tests and publishes
`SystemMonitor.exe` + `SystemMonitor.exe.sha256` to a GitHub Release. The CI build has no Intel Power
Gadget files; to ship a build with them, build locally and replace both assets on the release.

## Third-party Libraries

| Library | License | How bundled |
|---------|---------|-------------|
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 | Embedded resource |
| [PawnIO](https://pawnio.eu/) | see project | Embedded driver, installed as a service when needed |
| [Guna UI2](https://gunaui.com/) | Commercial | Embedded resource |

## License

MIT — see [LICENSE](LICENSE)
