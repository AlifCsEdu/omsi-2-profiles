# OMSI 2 Profile Manager - Complete AI Agent Prompt

## PROJECT OVERVIEW

Build a **native Windows 11 desktop application** that revolutionizes OMSI 2 addon management. The app must replace StartOmsi with a modern, fast, user-friendly alternative that uses the proven folder-hiding technique but with enterprise-grade architecture, pristine Windows 11 Fluent Design language, and zero external dependencies (portable .exe deployment).

**Core Problem Solved**: OMSI 2 addon profiles require tedious one-by-one folder selection. Users with 10+ profiles need instant profile switching with a modern UI.

**Solution Architecture**: Dynamic addon scanning + one-click profile loading via Windows folder renaming (non-destructive, reversible method proven by StartOmsi for 10+ years).

---

## TECHNOLOGY STACK (Latest 2025 Standards)

### Frontend Framework
- **WinUI 3 (Windows App SDK 1.6+)**
  - Use native Ahead-Of-Time (AOT) compilation for 50% faster startup and smaller package size
  - Latest Fluent Design System with Windows 11 native styling
  - Complete control over appearance without browser overhead
  - 45+ modern controls available for optimal UX

### Backend Language & Ecosystem
- **C# 13** with .NET 9.0
  - Modern async/await patterns (always use `async`/`await`, never `.Result` or `.Wait()`)
  - Full type safety with record types and pattern matching
  - ConfigureAwait(false) for non-UI contexts

### Project Structure & Build
- **Visual Studio 2022** project template: "WinUI 3 in Desktop (Unpackaged)"
  - Set `<WindowsPackageType>None</WindowsPackageType>` in .csproj
  - Remove Package.appxmanifest (not needed for unpackaged)
  - Use self-contained publish mode: `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`

### Deployment Model
- **Unpackaged + Self-Contained Application**
  - Results in folder with .exe + runtime dependencies (xcopy-deployable)
  - NOT a true single-file .exe (technically impossible with WinUI 3 + runtime), but achieves portable "zip and run" behavior
  - Publish to: `bin\Release\net9.0-windows10.0.22621.0\win-x64\publish`
  - All dependencies bundled next to executable; no installation required
  - Can be distributed via GitHub releases, cloud storage, USB drive
  - Users run single .exe from folder without system-wide installation

### Code Architecture Standards
- **MVVM pattern** (Model-View-ViewModel) for separation of concerns
- **Dependency Injection** using Microsoft.Extensions.DependencyInjection
- **Reactive bindings** with CaliburnMicro or direct XAML binding (avoid manual event handlers)
- **Async-first** design (all I/O operations must be non-blocking)
- **Logging** with Serilog or Microsoft.Extensions.Logging

### File & Folder Operations
- Use `System.IO` with async wrappers where possible
- Leverage `System.IO.Abstractions` for testability (mock file system in unit tests)
- All folder renames wrapped in try-catch with detailed error messages
- Use `WindowsIdentity` to detect if running with admin privileges (needed for validation only)

---

## FEATURE SPECIFICATION

### MVP (Phase 1) - Core Features

#### 1. OMSI 2 Installation Detection & Configuration
- **Automatic Detection on First Launch**
  - Scan common Steam locations (hardcoded paths):
    - `C:\Program Files (x86)\Steam\steamapps\common\OMSI 2`
    - `C:\Program Files\Steam\steamapps\common\OMSI 2`
    - `D:\SteamLibrary\steamapps\common\OMSI 2` (custom Steam library)
    - Registry lookup: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 16210` (OMSI 2 AppID)
  - If found automatically, display: "✓ Found at [path]"
  - If not found, show file picker: "Select your OMSI 2 installation folder"
  - Validate presence of `Vehicles/` and `Maps/` subdirectories
  - Store path in: `%APPDATA%\OMSIProfileManager\config.json`

- **Path Management**
  - Settings window allows user to change OMSI 2 path anytime
  - Trigger re-scan when path changes
  - Validate new path before saving

#### 2. Dynamic Addon Scanning Engine
- **Real-Time Folder Detection**
  - Scan `[OMSI2]\Vehicles\` directory
    - List all subdirectories NOT ending with `.disabled`
    - Extract folder names (these are addon identifiers)
    - Sort alphabetically for consistent display
    - Store in: `List<string> AvailableVehicles`
  
  - Scan `[OMSI2]\Maps\` directory
    - Same process as Vehicles
    - Store in: `List<string> AvailableMaps`
  
  - **Performance**: Run async in background to avoid UI blocking
  - **Caching**: Cache results in memory; refresh only on app startup or user request
  - Display count: "Detected: 47 buses, 12 maps"

- **Validation & Safety Checks**
  - Warn if scanned count differs dramatically from previous scan (addon deleted?)
  - Detect duplicate folder names across profiles (conflict warning)
  - Flag missing addons in profiles: "MAN_Lion_NL not found (was in profile)"
  - Store validation warnings in UI tooltip/status bar

#### 3. Profile Management System
- **Profile Data Model** (JSON storage)
  ```json
  {
    "version": "1.0",
    "lastUpdated": "2025-11-09T15:30:00Z",
    "profiles": [
      {
        "id": "prof_20251109_001",
        "name": "Berlin Daily",
        "description": "Casual Berlin driving, 5 buses",
        "vehicles": ["MAN_Lion_NL", "Iveco_Citylife", "Mercedes_Citaro_E"],
        "maps": ["Berlin_Spandau_v5"],
        "createdAt": "2025-11-09T10:00:00Z",
        "lastUsed": "2025-11-09T15:20:00Z",
        "tags": ["casual", "daily"],
        "enabled": true
      }
    ]
  }
  ```
  - Storage location: `%APPDATA%\OMSIProfileManager\profiles.json`
  - Backup location: `%APPDATA%\OMSIProfileManager\backups\profiles.json.[timestamp]`

- **Create New Profile**
  - Window: "New Profile" dialog
  - Input: Profile name (required, max 50 chars), description (optional, max 200 chars)
  - Vehicle selection: Checkbox list of all detected vehicles
  - Map selection: Single-select dropdown (only 1 map per profile)
  - Save button: Validate name uniqueness, serialize to JSON
  - Auto-generate unique ID: `prof_[timestamp]_[randomSuffix]`

- **Edit Profile**
  - Right-click profile → "Edit"
  - Modify name, description, vehicle list, map
  - Changes persist immediately to profiles.json

- **Delete Profile**
  - Right-click profile → "Delete"
  - Confirmation dialog: "Delete 'Berlin Daily' permanently?"
  - Remove from profiles.json, trigger UI refresh

- **List & Display Profiles**
  - Main window shows all profiles in a scrollable list
  - Each profile card displays:
    - Profile name (bold)
    - Description (grayed-out, smaller font)
    - Vehicle count badge (e.g., "5 buses")
    - Map name
    - Last used timestamp
    - "Load" button (primary action)
    - Context menu (Edit, Delete, Duplicate, Export)

#### 4. Profile Loading Engine (Core Logic)
- **Load Profile Flow** (when user clicks "Load" button)
  1. Validation phase:
     - Check if selected addons still exist on disk
     - Warn if any addons missing (e.g., "MAN_Lion_NL not found. Continue anyway?")
     - Allow user to proceed or cancel
  
  2. Disable phase:
     - Iterate all `[OMSI2]\Vehicles\*` folders
     - For each folder NOT ending in `.disabled`: rename to `[FolderName].disabled`
     - Handle exceptions: skip if folder is locked/in-use, show warning
     - Repeat for `[OMSI2]\Maps\*`
  
  3. Enable phase:
     - Iterate profile's vehicle list
     - For each vehicle: rename `[VehicleName].disabled` back to `[VehicleName]`
     - Handle missing: skip with silent log (not error)
     - Repeat for map
  
  4. Launch phase:
     - Start OMSI 2 via: `Process.Start("[OMSI2]\omsi.exe")`
     - Alternative if launcher exists: `Process.Start("[OMSI2]\launcher.exe")`
     - Track launch time, log success
  
  5. Cleanup (on app close):
     - Optional: ask user to restore all addons when exiting app
     - Default: leave active profile loaded (user can switch anytime)

- **Error Handling**
  - Folder rename fails (locked by another process): Show dialog "OMSI 2 is running. Close it and try again."
  - Profile has no map selected: Show error "Profile must have a map selected"
  - OMSI 2 path doesn't exist: Show error with option to change path

#### 5. Windows 11 UI Design (Fluent Design Language)
- **Main Window**
  - Title bar: "OMSI 2 Profile Manager" with app icon
  - Navigation pane (left sidebar):
    - Home (profile list)
    - Settings
    - About
  - Content area (right):
    - Profile list with search/filter
    - Status bar at bottom showing "Ready" / "Loading..." / "X addons active"

- **Main Content Area**
  - Search box: Real-time filter profiles by name/description
  - Sort options: By name, by date created, by last used
  - Profile list: Scrollable grid of profile cards
  - Each card:
    - Shadow effect (Fluent depth)
    - Hover state: Highlight, show "Load" button
    - Context menu icon (⋯) in top-right
  - Empty state: If no profiles exist, show "No profiles yet. Create one to get started." with "New Profile" button

- **Profile Card Actions**
  - Primary: "Load" button (blue accent color)
  - Secondary: "⋯" menu → Edit, Duplicate, Delete, Export JSON
  - Double-click to load (also valid)

- **New/Edit Profile Dialog**
  - Modal window with form controls
  - Fields:
    - Name (TextBox, required)
    - Description (TextBox multiline, optional)
    - Vehicle selection (GridView with checkboxes, scrollable)
    - Map selection (ComboBox dropdown, single select)
  - Buttons: Save (primary), Cancel (secondary)
  - Validation: Show inline error if name is empty or duplicate

- **Settings Window**
  - OMSI 2 Installation Path
    - Display current path
    - "Change Path..." button → file picker → validate → re-scan
  - Preferences
    - Auto-backup before loading profile (default: ON)
    - Confirm before loading (default: OFF)
    - Show toast notifications (default: ON)
  - About
    - App version, build date
    - GitHub repo link
    - License info

- **Color Scheme**
  - Light mode: White backgrounds, dark text (default for Windows 11)
  - Dark mode: Auto-follow system theme setting
  - Accent color: Windows 11 accent color (from system settings)
  - Controls: Use WinUI 3 controls exclusively (Button, TextBox, ComboBox, etc.)

#### 6. Backup & Safety
- **Auto-Backup Before Profile Load**
  - Before disabling addons, capture current addon state
  - Save to: `%APPDATA%\OMSIProfileManager\backups\backup_[timestamp].json`
  - Keep last 10 backups; auto-delete older ones
  - Format: Simple list of currently active vehicle/map folders
  
- **Restore Functionality**
  - Settings menu: "Restore from backup"
  - Show list of recent backups with timestamps
  - Click to restore (re-enable those addons, disable others)

- **Crash Recovery**
  - On app startup: Check if previous load was interrupted
  - If .disabled folders are in inconsistent state: Notify user, offer to "Restore Last Known Good State"

#### 7. Logging & Diagnostics
- **Logging System** (Serilog)
  - Log file: `%APPDATA%\OMSIProfileManager\logs\app_[date].log`
  - Log all operations:
    - Profile load/create/delete
    - Folder rename operations (success & failure)
    - OMSI 2 process launch
    - Validation warnings
  - Log level: Info (default), Debug (for troubleshooting)
  - Rotation: New log file daily, keep 30 days

---

## TECHNICAL IMPLEMENTATION DETAILS

### Core Classes & Architecture

#### Data Layer
```
Models/
├── Profile.cs               // Data model for profiles
├── AddonInfo.cs            // Vehicle/Map metadata
├── AppConfig.cs            // User settings & OMSI 2 path
└── BackupState.cs          // Backup snapshot
```

#### Service Layer
```
Services/
├── IProfileManager.cs       // Interface
├── ProfileManager.cs        // Load/save profiles.json
├── IAddonScanner.cs        // Interface
├── AddonScanner.cs         // Scan Vehicles/ and Maps/
├── IOMSILauncher.cs        // Interface
├── OMSILauncher.cs         // Folder operations & launch
├── IBackupManager.cs       // Interface
├── BackupManager.cs        // Backup/restore logic
└── ILoggingService.cs      // Logger setup
```

#### UI Layer (MVVM)
```
ViewModels/
├── MainWindowViewModel.cs     // Main profile list logic
├── NewProfileViewModel.cs     // Create/edit dialog logic
├── SettingsViewModel.cs       // Settings page logic
└── ShellViewModel.cs          // Navigation pane
Views/
├── MainWindow.xaml           // Main UI
├── MainWindow.xaml.cs
├── NewProfileWindow.xaml     // Create/edit dialog
├── NewProfileWindow.xaml.cs
├── SettingsWindow.xaml
├── SettingsWindow.xaml.cs
└── AppResources.xaml         // Fluent Design resources
```

#### Entry Point
```
App.xaml.cs                    // Bootstrapping, DI setup
```

### Async/Await Pattern Compliance
- **Never use**: `.Result`, `.Wait()`, `Task.Run()` for CPU-bound blocking
- **Always use**: `async`/`await` throughout call stack
- **File I/O**: `File.ReadAllTextAsync()`, `Directory.EnumerateDirectoriesAsync()` (or use System.IO.Abstractions)
- **Process launch**: Use `ProcessStartInfo` with `UseShellExecute = false` for async-friendly execution
- **UI updates**: Dispatch back to UI thread only when necessary (WinUI 3 handles most automatically)

### Error Handling Strategy
- **Try-catch blocks** around all folder operations
- **User-facing errors**: Show dialog with clear message (not exception stack trace)
- **System errors**: Log to file, offer "View Logs" button
- **Validation errors**: Show inline in UI (e.g., "Profile name already exists")

### Performance Optimization
- **Lazy loading**: Load profile list on demand (don't scan all files on startup unless necessary)
- **Caching**: Keep addon list in memory; only re-scan on app refresh or path change
- **Native AOT compilation**: Publish with AOT enabled for fastest startup
- **Minimal dependencies**: Avoid large NuGet packages; use built-in .NET libraries where possible

### Security Considerations
- **File paths**: Validate all paths before use (prevent directory traversal)
- **Folder renaming**: Only touch `Vehicles/` and `Maps/` subdirectories; never touch base game files
- **Admin elevation**: NOT required (renaming user-owned files is allowed)
- **Backups**: Store in `%APPDATA%` which is user-owned; no system-wide access needed

---

## GITHUB CODESPACE SETUP & WORKFLOW

### Initial Setup
1. Create new Codespace from GitHub repo
2. Codespace should have Visual Studio Build Tools pre-installed (or install via script)
3. Clone repo: `git clone [your-repo]`
4. Restore NuGet packages: `dotnet restore`
5. Build: `dotnet build`

### Development Environment
- **IDE**: Use Visual Studio Code (Codespace-compatible)
- **Language**: C# 13
- **.NET Version**: .NET 9.0
- **Build Target**: `net9.0-windows10.0.22621.0` (Windows 11 21H2+)
- **SDK**: Windows App SDK 1.6.x (specify in .csproj)

### NuGet Package Dependencies (Minimal)
```xml
<ItemGroup>
  <!-- WinUI 3 & Windows App SDK -->
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.x" />
  <PackageReference Include="Microsoft.Xaml.Compiler" Version="9.x" />
  
  <!-- Async & Utilities -->
  <PackageReference Include="System.IO.Abstractions" Version="22.x" />
  
  <!-- Logging -->
  <PackageReference Include="Serilog" Version="4.x" />
  <PackageReference Include="Serilog.Sinks.File" Version="6.x" />
  
  <!-- DI -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.x" />
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.x" />
  
  <!-- JSON -->
  <PackageReference Include="System.Text.Json" Version="9.x" />
</ItemGroup>
```

### Build & Publish Commands
```bash
# Development build (debug)
dotnet build -c Debug

# Production build (release)
dotnet build -c Release

# Publish self-contained unpackaged app
dotnet publish -c Release -o bin/publish

# Publish with Native AOT (requires .NET 9.0 AOT toolchain)
dotnet publish -c Release /p:PublishAot=true -o bin/publish-aot
```

### GitHub Actions CI/CD (Optional)
- Trigger on push to `main` branch
- Build & publish automatically
- Create GitHub Release with `.zip` containing publish output
- Users download `.zip`, extract, run `.exe`

---

## USER WORKFLOW (How End-User Experiences App)

### First Time
1. Launch `OMSIProfileManager.exe`
2. App auto-detects OMSI 2 installation
3. Scans addons: "Detected 47 buses, 12 maps"
4. Shows empty profile list
5. User clicks "New Profile"
6. Dialog: Enter name "Berlin Daily", select 5 buses + Berlin map
7. Click "Save"
8. Profile appears in main list
9. Repeat for 9 more profiles

### Daily Use
1. Launch `OMSIProfileManager.exe`
2. Sees list of 10 profiles
3. Clicks "Load" on "Berlin Daily"
4. App renames folders in background (~1-2 seconds)
5. OMSI 2 launches automatically
6. Game loads with only Berlin map + 5 buses (fast!)
7. Play game
8. Close game
9. Close app (or leave running)
10. Tomorrow: repeat step 3

### Adding New Addon
1. User installs new bus mod to `C:\OMSI 2\Vehicles\NewBus`
2. Launches app (app auto-detects new bus in scan)
3. Clicks "Edit" on a profile
4. New bus appears in vehicle checklist
5. Check the box, save
6. Profile now includes new bus

---

## QUALITY & BEST PRACTICES CHECKLIST

### Code Standards
- ✅ No hardcoded strings (use constants or resource files)
- ✅ No async void methods (only async Task)
- ✅ No .Result or .Wait() blocking calls
- ✅ Null checks with `ArgumentNullException.ThrowIfNull()`
- ✅ Comprehensive XML documentation comments on public APIs
- ✅ Unit tests for business logic (ServiceLayer)
- ✅ Integration tests for file I/O operations

### UI/UX Standards
- ✅ Fluent Design: Shadows, acrylic, reveal effects
- ✅ Keyboard navigation: Tab through all controls, Enter to confirm
- ✅ Dark mode support: Test with Windows 11 dark theme
- ✅ High DPI support: Test at 100%, 125%, 150% scaling
- ✅ Accessibility: ARIA labels, screen reader friendly
- ✅ Empty states: Friendly messages when no profiles exist
- ✅ Loading states: Show spinner/progress bar during addon scan
- ✅ Error messages: Clear, actionable (not technical jargon)

### Performance
- ✅ App startup: < 2 seconds (Native AOT target)
- ✅ Profile load: < 1.5 seconds (folder rename operations)
- ✅ Addon scan: < 500ms (cached, async)
- ✅ Memory footprint: < 100MB at runtime
- ✅ No UI blocking during I/O operations

### Reliability
- ✅ Graceful shutdown: Save state before closing
- ✅ Error recovery: Backup & restore on corruption
- ✅ Logging: All operations logged for debugging
- ✅ File locking: Detect if OMSI 2 running, prompt to close
- ✅ Path validation: Prevent directory traversal & invalid paths

---

## DEPLOYMENT & DISTRIBUTION

### Package Structure
```
OMSIProfileManager/
├── OMSIProfileManager.exe          (Main executable)
├── OMSIProfileManager.dll          (App assembly)
├── Microsoft.*.dll                 (Framework dependencies)
├── WinAppRuntime.dll              (WinUI 3 runtime)
└── runtimes/                       (Platform-specific libraries)
```

### Installation Method
- **For Users**: Download `.zip` from GitHub Releases
  - Extract to folder (e.g., `C:\Tools\OMSIProfileManager`)
  - Run `OMSIProfileManager.exe`
  - Optional: Create shortcut to Start Menu or Desktop
  - **No system installation required; no admin rights needed**

### GitHub Releases
- Trigger: Tag commit as `v1.0.0`, push to GitHub
- GitHub Actions auto-build & create release with `.zip`
- Users see release on GitHub with download button
- Include README with setup instructions

---

## EDGE CASES & ASSUMPTIONS

### Assumptions
1. OMSI 2 is installed on user's PC (not mandatory; app asks for path if not found)
2. User has permission to rename folders in `C:\Games\OMSI 2\` (true for own folders)
3. Windows 11 is running (no Windows 10 support; app targets Win11 APIs)
4. User has .NET 9 runtime installed OR app published self-contained (includes runtime)

### Edge Cases Handled
1. **OMSI 2 is running**: App detects locked folders, shows "Close OMSI 2 and try again"
2. **Addon folder missing**: Profile loads anyway, skips missing addon (with warning)
3. **Duplicate profile names**: Validation prevents saving
4. **Addon deleted after profile creation**: Warns on load, allows continue
5. **Corrupted profiles.json**: Auto-repair or reset to empty state
6. **Multiple instances of app running**: File locking prevents simultaneous profile changes
7. **Very large addon count (100+)**: Scanning may take 2-3 seconds; show progress
8. **Custom Steam library paths**: Registry lookup finds non-standard Steam locations
9. **Portable OMSI 2 (not via Steam)**: File picker allows manual path selection

---

## FINAL CHECKLIST BEFORE HANDOFF TO AI AGENT

- ✅ Tech stack specified: WinUI 3, C# 13, .NET 9.0, Windows App SDK 1.6, Native AOT
- ✅ Architecture defined: MVVM, async-first, DI container, Serilog logging
- ✅ UI/UX specified: Fluent Design, Windows 11 native, all major screens defined
- ✅ Feature scope clear: Profile CRUD, addon scanning, folder renaming logic
- ✅ Deployment model explicit: Unpackaged self-contained .exe + DLLs (portable)
- ✅ Error handling strategy: Try-catch, validation, user-friendly messages
- ✅ Performance targets: Sub-2s startup (AOT), sub-1.5s profile load
- ✅ Security reviewed: No admin elevation needed, file paths validated
- ✅ Best practices: Async/await, no blocking calls, comprehensive logging
- ✅ Testing approach: Unit tests (services), integration tests (file I/O)
- ✅ Deployment automated: GitHub Actions CI/CD, automated releases
- ✅ All edge cases documented with handling strategy

---

## RESEARCH CITATIONS & STANDARDS

### Latest Technologies Used (2025)
- **WinUI 3 / Windows App SDK 1.6** [Leading Windows development framework; native AOT support reduces startup 50%][154][167][170][173]
- **C# 13 async/await patterns** [Modern asynchronous best practices; avoid blocking calls][169][172][175]
- **GitHub Copilot prompt engineering** [Multi-step prompting, example-based patterns, context hooks improve acceptance rate 70-80%][158][161][164]
- **Native AOT compilation** [Produces faster startup times and smaller memory footprint vs JIT][167][170][173][176]
- **Fluent Design System** [Windows 11 native design language; implemented via WinUI 3 controls][168][171]
- **Self-contained portable deployment** [Windows App SDK 1.6 supports self-contained unpackaged apps; .exe + runtime in folder][159][162][165]

---

## PROMPT COMPLETION SIGNAL

This specification is **complete and production-ready**. All ambiguity has been resolved. The AI agent has:
- Clear technical stack (no alternatives)
- Detailed feature list (no scope creep)
- Explicit file paths & data formats
- Performance targets
- Error handling strategy
- Deployment mechanism
- Best practices checklist

**Ready to code**: Implement MVP features in sequence: (1) OMSI 2 path detection, (2) addon scanner, (3) profile CRUD, (4) folder rename logic, (5) WinUI 3 UI, (6) settings/logging.

**Code-first approach**: Build backend services first (testable), then wire UI last (faster iteration, fewer blocking issues).