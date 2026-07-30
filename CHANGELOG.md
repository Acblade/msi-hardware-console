# Changelog

## 0.1.5

- Promoted Thermal guard to a top-level section between Fan control and Startup
- Ordinary fan-curve charts now show only their usable 0–60% range
- Expanded adjustable guard ranges to 85–95°C sustained, 90–100°C immediate, and 70–92°C release

## 0.1.4

- Moved thermal-guard controls into a dedicated panel below Fan control
- Redesigned the three temperature controls with clearer timing, actions, ranges, and visual hierarchy
- Rewrote all fan-mode descriptions around their actual behavior and recommended use

## 0.1.3

- Ordinary fan curves are capped at 60%; 100% is reserved for manual Full Blast or the independent thermal guard
- Added adjustable sustained, immediate, and release temperatures beside the Fan control heading
- Added safe ranges and automatic 3°C separation between thermal-guard thresholds
- Removed thermal-guard details from fan-curve overlays

## 0.1.2

- The title-bar minimize button now keeps the app on the Windows taskbar
- Closing a maximized window to the notification area preserves the maximized state when reopened

## 0.1.1

- Fixed mode now uses a true on/off toggle and disables its duty slider while the fan is off
- Automatic mode no longer shows fabricated fan-duty percentages
- Full Blast protection now requires sustained high temperature, while extreme heat still triggers it immediately
- Updated English and Simplified Chinese UI copy and documentation

## 0.1.0

- First public preview
- English-default UI with instant Simplified Chinese switching
- CPU, discrete GPU, integrated GPU, temperature, storage, and RPM dashboard
- In-window performance and fan-curve overlays
- Verified fan profiles for MSI Cyborg 15 A13VE with MSI WMI 2.8
- Monitoring-only safety lock on unverified hardware
- Portable Windows x64 build and elevated notification-area startup
