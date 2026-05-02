# ClickityClackity

A lightweight Windows desktop app that plays sounds on every keystroke, mouse click, scroll, and drag — globally, no matter which window is focused.

Assign any audio file to any input event. Stack multiple sounds per event for randomized playback. Fine-tune pitch, volume, and per-key overrides to build your own custom soundscape.

---

## Features

- **Global input hooks** — works in any app, no focus required
- **All input events covered**
  - Keyboard: key down, key up, key hold
  - Mouse buttons: left / right / middle — down and up
  - Mouse movement: scroll up / down, left drag, right drag, middle drag
- **Multiple sounds per event** — assign a list and one is picked at random on each trigger
- **Per-key sound overrides** — assign a different sound to a specific key, with optional Ctrl / Alt / Shift modifier matching
- **Hold sounds** — separate sound for when a key is held, per override
- **Drag pitch shifting** — mouse drag direction controls pitch in real time (right/up = higher, left/down = lower)
- **Random pitch variation** — per-sound toggle to add ± N semitone variation on each play
- **Per-event volume control** — independent volume for every event type
- **Configurable sounds folder** — point the app at any folder on your machine
- **System tray** — minimizes to tray, enable/disable from the tray icon right-click menu
- **Profile persistence** — all settings saved to `profile.json` next to the exe

---

## Download

Grab the latest single-file `.exe` from the [**Releases**](https://github.com/arthurstreeter/ClickityClackity/releases) page.

No installer. No runtime to install. Just download, drop your sounds in, and run.

---

## Quick Start

1. **Download** `ClickityClackity.exe` from [Releases](https://github.com/arthurstreeter/ClickityClackity/releases)
2. Put it in its own folder (e.g. `C:\Apps\ClickityClackity\`)
3. **Add sounds** — create a `sounds\` folder next to the exe and drop in your audio files  
   *(supported formats: `.wav` `.mp3` `.flac` `.aiff` `.m4a` `.aac`)*  
   Or use **Browse…** in the app to point it at any existing folder
4. Launch `ClickityClackity.exe`
5. Use the dropdowns to assign sounds to events
6. Close the window — it keeps running in the system tray

> **Tip:** Hit **Refresh** after adding new files to the sounds folder so they appear in the dropdowns.

---

## Usage Guide

### Main Window

| Control | What it does |
|---|---|
| **● Active / ○ Inactive** toggle | Enable or disable all sound playback |
| **Volume** slider | Master volume (0 – 100 %) |
| **Sounds** + **Browse…** | Configure which folder to scan for audio files |
| Event dropdowns | Assign one or more sounds to each input event |
| **+ add sound** | Stack additional sounds on an event (one plays at random) |
| **±** button next to a sound | Purple = random pitch applies to this sound; gray **–** = pitch locked |
| **Advanced** | Open the Advanced Settings window |
| **Open Sounds Folder** | Open the current sounds folder in Explorer |
| **Refresh** | Rescan the sounds folder for new files |

### Advanced Settings

#### Per-Event Volumes
Individual volume sliders for every event (0 – 200 %).

#### Per-Key Sound Overrides
Assign a completely different sound (or sound list) to a specific key.

1. Click **+ Add Key Override**
2. Press any key on your keyboard
3. The new override row appears — assign sounds using the dropdowns
4. Toggle **Ctrl** / **Alt** / **Shift** buttons to require those modifiers (e.g. make Ctrl+Z play a different sound than plain Z)
5. The **↓ DOWN** panel controls what plays on key press; **⟳ HOLD** controls what plays while the key is held (leave empty to use the global hold sound)

#### Pitch Settings

| Setting | What it does |
|---|---|
| **Drag pitch range** | How many semitones the pitch shifts at maximum drag speed. Set to 0 to disable. |
| **Random variation** | Randomises pitch by ± N semitones on every sound played (global range; per-sound **±** toggle enables/disables it per sound) |

### System Tray

Right-click the tray icon to **Enable / Disable** playback without opening the window, or to **Exit** the app.

---

## Building from Source

### Prerequisites

| Tool | Version | Download |
|---|---|---|
| Windows | 10 or 11 (64-bit) | — |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 or later | dotnet.microsoft.com |
| [Git](https://git-scm.com/downloads) | Any recent version | git-scm.com |

> **Check if you already have .NET 8:**  
> Open a terminal and run `dotnet --version`. If it shows `8.x.x` or higher you're good.

---

### Step 1 — Clone the repository

Open **Command Prompt** or **PowerShell** and run:

```
git clone https://github.com/arthurstreeter/ClickityClackity.git
cd ClickityClackity
```

---

### Step 2 — Build and run (for development)

```
dotnet run
```

This compiles the project and launches the app directly. Use this while developing.

---

### Step 3 — Publish a self-contained single-file exe

This produces one `.exe` that bundles the entire .NET runtime — no installation required on the target machine.

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The output is in the `publish\` folder:

```
publish\
  ClickityClackity.exe   ← the app (everything bundled in)
  sounds\                ← drop your audio files here
```

> **Note:** The first launch extracts a small set of WPF rendering libraries to `%LocalAppData%\Temp\.net\ClickityClackity\`. This is normal and only happens once per version.

---

### Project Structure

```
ClickityClackity/
├── Assets/
│   ├── icon_enabled.ico       # Tray icon — active state (also the exe icon)
│   └── icon_disabled.ico      # Tray icon — inactive state
│
├── Models/
│   ├── InputEvent.cs          # Enum of all hookable events + helper extensions
│   ├── InputEventData.cs      # Event payload (vk code, mouse delta, modifier state)
│   ├── KeyOverrideEntry.cs    # Per-key override model
│   └── SoundEntry.cs          # Sound file + random-pitch flag
│
├── Services/
│   ├── NativeMethods.cs       # P/Invoke declarations (SetWindowsHookEx etc.)
│   ├── InputHookService.cs    # Global keyboard + mouse LL hooks
│   ├── SoundEngine.cs         # NAudio playback engine with pitch shifting
│   └── SoundManager.cs        # Sound map, profile load/save, play routing
│
├── Views/
│   ├── AdvancedWindow.xaml
│   └── AdvancedWindow.xaml.cs # Per-event volumes, key overrides, pitch settings
│
├── Themes/
│   └── DarkTheme.xaml         # Catppuccin Mocha resource dictionary
│
├── App.xaml / App.xaml.cs     # App entry point, system tray setup
├── MainWindow.xaml / .cs      # Main UI
└── ClickityClackity.csproj
```

---

### Dependencies

| Package | Purpose |
|---|---|
| [NAudio](https://github.com/naudio/NAudio) 2.2.1 | Low-latency audio playback with pitch shifting |

All other APIs used (`System.Windows.Forms` for the tray icon, `System.Drawing` for icons, Win32 P/Invoke for global hooks) are part of Windows / the .NET runtime.

---

## Sounds

The app ships with no sounds — you bring your own.

**Where to get sounds:**
- Export clips from a DAW or audio editor
- Download from sites like [freesound.org](https://freesound.org) or [zapsplat.com](https://www.zapsplat.com)
- Rip UI sounds from games you own

Put your files in the configured sounds folder, hit **Refresh**, and they'll appear in every dropdown.

---

## License

MIT — do whatever you like with it.
