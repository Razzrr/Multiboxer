# Multiboxer

A customizable multiboxing application for Windows, built with C# and WPF. This is a full-featured replacement for Joe Multiboxer with complete configurability and source code access.

## Features

### Core Features
- **Slot Management**: Run up to 72 game instances simultaneously
- **Window Layouts**: Automatic window arrangement (Horizontal, Vertical, Custom)
- **Global Hotkeys**: F1-F12 for slot switching, customizable navigation keys
- **Highlighter Overlay**: Red border on active window, slot numbers on background windows
- **Multi-Monitor Support**: Target specific monitors for window layouts
- **Profile Management**: Save and load game launch configurations

### Advanced Features
- **Custom Layout Editor**: Create and edit custom window layouts with visual editor
- **Virtual File Redirection**: Per-slot configuration files (e.g., `eqclient.ini` → `eqclient.1.ini`)
- **CPU Affinity Control**: Pin processes to specific CPU cores for performance
- **Launcher Detection**: Automatic window detection for games using launchers
- **Deferred Window Positioning**: Reduces DirectX reset errors during window resizing
- **System Tray**: Minimize to tray with quick access menu
- **Import/Export**: Backup and restore all settings

## Requirements

### For Running
- Windows 10 or later (x64)
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Administrator privileges (required for virtual file redirection)

### For Building
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (optional, for IDE development)

## Building

### Option 1: Command Line (Recommended)

```bash
# Clone the repository
git clone https://github.com/Razzrr/Multiboxer.git
cd Multiboxer

# Restore dependencies
dotnet restore

# Build in Debug mode
dotnet build

# Build in Release mode
dotnet build -c Release

# Run the application
dotnet run --project src/Multiboxer.App
```

### Option 2: Visual Studio 2022

1. Open `Multiboxer.sln` in Visual Studio 2022
2. Restore NuGet packages (automatic or via Tools → NuGet Package Manager → Restore)
3. Select **Debug** or **Release** configuration
4. Build solution (`Ctrl+Shift+B`)
5. Run with `F5` (debug) or `Ctrl+F5` (without debugging)

### Publishing a Release Build

Create a self-contained executable that doesn't require .NET runtime installed:

```bash
# Single-file executable (recommended for distribution)
dotnet publish src/Multiboxer.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish

# Framework-dependent (smaller size, requires .NET 8.0 runtime)
dotnet publish src/Multiboxer.App -c Release -r win-x64 --self-contained false -o ./publish
```

Output will be in the `./publish` folder.

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test -v normal

# Run specific test project
dotnet test tests/Multiboxer.Tests
```

### Build Output Locations

| Configuration | Location |
|---------------|----------|
| Debug | `artifacts/bin/Multiboxer.App/debug/` |
| Release | `artifacts/bin/Multiboxer.App/release/` |
| Published | `./publish/` (or custom -o path) |

## Project Structure

```
Multiboxer/
├── src/
│   ├── Multiboxer.Native/     # Win32 P/Invoke layer
│   │   ├── User32.cs          # Window APIs
│   │   ├── Kernel32.cs        # Process/CPU APIs
│   │   └── Dwmapi.cs          # DWM composition
│   ├── Multiboxer.Core/       # Core business logic
│   │   ├── Slots/             # Slot management
│   │   ├── Window/            # Window manipulation
│   │   ├── Layout/            # Layout strategies
│   │   ├── Input/             # Hotkey handling
│   │   ├── Performance/       # CPU affinity
│   │   ├── VirtualFiles/      # File redirection
│   │   └── Config/            # Settings management
│   ├── Multiboxer.Overlay/    # Highlighter overlays
│   └── Multiboxer.App/        # WPF application
│       ├── Views/             # Dialogs and views
│       └── Services/          # App services
├── tests/
│   └── Multiboxer.Tests/      # xUnit test suite (23 test files)
└── config/                    # Default configurations
```

## Architecture

### Layer Overview

| Layer | Project | Purpose |
|-------|---------|---------|
| **Native** | Multiboxer.Native | Win32 P/Invoke wrappers (User32, Kernel32, Dwmapi) |
| **Core** | Multiboxer.Core | Business logic: slots, layouts, hotkeys, config |
| **Overlay** | Multiboxer.Overlay | Visual highlights and DWM thumbnail windows |
| **App** | Multiboxer.App | WPF UI with MVVM pattern |

### Key Components

| Component | File | Purpose |
|-----------|------|---------|
| SlotManager | `Core/Slots/SlotManager.cs` | Central coordinator for up to 72 game slots |
| LayoutEngine | `Core/Layout/LayoutEngine.cs` | ISBoxer-style window layout and positioning |
| HotkeyManager | `Core/Input/HotkeyManager.cs` | Global hotkey handling (API + low-level hooks) |
| OverlayManager | `Overlay/OverlayManager.cs` | Visual overlay coordination |
| ConfigManager | `Core/Config/ConfigManager.cs` | Settings persistence and I/O |

### Component Interaction Flow

```
User Input (Hotkey F1-F12)
    │
    ▼
HotkeyManager (low-level keyboard hook or WM_HOTKEY)
    │
    ▼
App.xaml.cs (event handler)
    │
    ▼
SlotManager.FocusSlot(slotId)
    │
    ▼
Slot.Focus() → Brings window to foreground
    │
    ▼
LayoutEngine.SwapLayoutOnFocus(slotId)
    ├─ Apply borderless mode (optional)
    ├─ Calculate window positions
    ├─ Park background windows off-screen (if thumbnails enabled)
    └─ Batch apply positions using DeferWindowPos
    │
    ▼
OverlayManager updates overlay windows
    ├─ Red border on active window
    └─ Slot numbers on background windows
```

### Technical Implementation Details

- **Window Claiming System**: Static `_claimedWindows` HashSet prevents multiple slots from grabbing the same window during batch launches
- **ISBoxer-Style Regions**: Each slot has ForeRegion (main window) and BackRegion (thumbnail) - layout swaps positions on focus
- **Window Parking**: Background windows are moved far off-screen (-2000,-2000) when using thumbnails to avoid visual corruption
- **Batch Positioning**: Uses `DeferWindowPos` for atomic window updates, reducing flicker and DirectX errors
- **Dual Hotkey Modes**: Supports both Windows `RegisterHotKey` API and low-level keyboard hooks for better game compatibility
- **DPI-Aware**: Uses monitor working area and DPI scaling for multi-monitor setups

## Default Hotkeys

| Key | Action |
|-----|--------|
| F1-F12 | Focus slots 1-12 |
| End | Focus slot 13 |
| Ctrl+Alt+Z | Previous window |
| Ctrl+Alt+X | Next window |

## Layout Options

- **Swap when Focused**: Automatically swap window positions when clicking a background window
- **Swap when Focused by Hotkey**: Swap positions when using hotkeys
- **Leave Hole**: Don't move the previous main window when swapping
- **Avoid Taskbar**: Keep windows within the working area
- **Make Borderless**: Remove window borders for seamless layout
- **Rescale Windows**: Use deferred positioning to avoid DirectX issues

## Layout Types

### Built-in Layouts
- **Horizontal**: Main window on top, others tiled horizontally at bottom
- **Vertical**: Main window on left, others tiled vertically on right

### Custom Layouts
Create custom layouts with the built-in editor:
- **Grid layouts**: 2x2, 3x3, or any grid configuration
- **PiP (Picture-in-Picture)**: Main fullscreen with small overlays
- **Custom regions**: Define exact position and size for each slot

## Configuration

Settings are stored in:
```
%APPDATA%\Multiboxer\
├── settings.json              # Main settings
├── profiles/                  # Game profiles
├── layouts/                   # Custom layouts
└── virtualfiles_backup/       # Original file backups
```

## Creating a Game Profile

1. Go to **Launcher** tab
2. Click **Manage...**
3. Click **New**
4. Enter:
   - **Profile Name**: Display name
   - **Game**: Game identifier (e.g., "EverQuest")
   - **Path**: Game installation folder
   - **Executable**: Launcher or game .exe file
   - **Game Executable** (optional): Actual game .exe if using a launcher
5. Configure optional settings:
   - **Window Class/Title Pattern**: For reliable window detection
   - **Virtual Files**: Enable per-slot configuration files
6. Click **Save**

### EverQuest Example Profile

```
Profile Name: EverQuest
Game: EverQuest
Path: C:\Games\EverQuest
Executable: LaunchPad.exe
Game Executable: eqgame.exe
Window Class: EverQuestWindowClass
Virtual Files: eqclient.ini
```

## Virtual File Redirection

Virtual file redirection allows each game instance to use its own configuration file.

### How It Works
1. Original file (e.g., `eqclient.ini`) is backed up
2. Symlink is created: `eqclient.ini` → `eqclient.1.ini`
3. Game reads/writes to slot-specific file
4. On exit, original file is restored

### Setup
1. Edit your game profile
2. Check "Enable per-slot configuration files"
3. Add file patterns (e.g., `eqclient.ini`)
4. Run Multiboxer as Administrator

### Requirements
- Administrator privileges, OR
- Windows Developer Mode enabled

## Multi-Monitor Support

1. Go to **Layout** tab
2. Select target monitor from dropdown
3. All window positioning will use the selected monitor

## Troubleshooting

### "HandleResolutionChanged() ResetDevice() failed" (EverQuest)
This error occurs when DirectX can't handle rapid window resizing:
- Enable "Rescale Windows" option (uses deferred positioning)
- The application automatically uses `SWP_ASYNCWINDOWPOS` flag

### Virtual Files Not Working
- Run Multiboxer as Administrator
- Or enable Windows Developer Mode (Settings → Update & Security → For Developers)

### Game Windows Not Detected
- Configure "Window Class" in profile settings
- For EverQuest: `EverQuestWindowClass`
- Use "Game Executable" field if using a launcher

### Hotkeys Not Working in Game
- The application uses low-level keyboard hooks
- Some anti-cheat software may block this
- Try running Multiboxer as Administrator

## Testing

The project includes comprehensive unit tests using xUnit.

### Test Framework
- **xUnit** (v2.6.2) - Test framework
- **FluentAssertions** (v6.12.0) - Fluent assertion library
- **Moq** (v4.20.70) - Mocking framework

### Test Coverage
Tests are organized by module in `tests/Multiboxer.Tests/`:

| Module | Coverage |
|--------|----------|
| Config | AppSettings, ConfigManager, EqIniManager, LaunchProfile |
| Input | HotkeyBinding, HotkeyManager, VirtualKeys |
| Layout | LayoutCalculator, LayoutOptions, LayoutStrategy, WindowRegion |
| Native | Constants, Enums, Structs |
| Overlay | ThumbnailManager |
| Performance | AffinityManager |
| Slots | SlotManager, SlotState, Slot |
| Window | MonitorManager, Rectangle, WindowHelper |

### Running Tests
```bash
dotnet test
```

## Dependencies

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (v8.2.2) - MVVM framework
- [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json) (v8.0.5) - JSON serialization
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) (v1.1.0) - System tray icon

## Comparison with Joe Multiboxer

| Feature | Joe Multiboxer | Multiboxer |
|---------|---------------|------------|
| Slot Management | ✅ | ✅ |
| Window Layouts | ✅ | ✅ |
| Custom Layouts | ✅ | ✅ (with visual editor) |
| Global Hotkeys | ✅ | ✅ |
| Highlighter | ✅ | ✅ |
| Virtual Files | ✅ | ✅ |
| Multi-Monitor | ✅ | ✅ |
| CPU Affinity | ✅ | ✅ |
| FPS Limiting | ✅ | ❌ (by design) |
| Source Code | ❌ | ✅ |
| Customizable | Limited | Fully |

## License

MIT License - feel free to modify and distribute.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Acknowledgments

Inspired by Joe Multiboxer (Inner Space) - this project aims to provide an open-source alternative with full customization capabilities.
