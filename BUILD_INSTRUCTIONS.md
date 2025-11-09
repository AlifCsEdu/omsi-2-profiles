# Building OMSI 2 Profile Manager

## ⚠️ Important: WinUI 3 Build Requirements

This application uses **WinUI 3** (Windows App SDK), which **requires Windows** to build due to the XAML compiler (`XamlCompiler.exe`) being a Windows-only tool.

## ✅ Option 1: Build on Windows (Recommended)

### Prerequisites
- Windows 10/11
- Visual Studio 2022 (17.8+) with:
  - .NET desktop development workload
  - Windows App SDK C# Templates
- OR .NET 8 SDK with Windows 11 SDK (10.0.22621.0+)

### Build Commands
```bash
# Restore NuGet packages
dotnet restore

# Build Debug
dotnet build -c Debug

# Build Release
dotnet build -c Release -r win-x64 --self-contained true

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained true -o bin/publish
```

### Output Location
- Debug: `bin/Debug/net8.0-windows10.0.22621.0/win-x64/OMSIProfileManager.exe`
- Release: `bin/Release/net8.0-windows10.0.22621.0/win-x64/OMSIProfileManager.exe`
- Publish: `bin/publish/OMSIProfileManager.exe`

---

## ✅ Option 2: Use GitHub Actions (Build in Cloud)

Create `.github/workflows/build.yml`:

```yaml
name: Build WinUI 3 App

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build -c Release -r win-x64 --self-contained true --no-restore
    
    - name: Publish
      run: dotnet publish -c Release -r win-x64 --self-contained true -o publish --no-build
    
    - name: Upload artifacts
      uses: actions/upload-artifact@v4
      with:
        name: OMSIProfileManager-Windows-x64
        path: publish/
```

After pushing, download the built `.exe` from the Actions tab.

---

## ✅ Option 3: Use Remote Windows Machine

### Via Azure/AWS Windows VM
1. Spin up a Windows Server 2022 VM
2. Install .NET 8 SDK
3. Clone the repository
4. Run build commands
5. Download the built exe

### Via Windows Sandbox (Local Windows)
1. Enable Windows Sandbox
2. Copy project folder into Sandbox
3. Install .NET 8 SDK
4. Build and copy exe out

---

## ❌ Why Linux Build Doesn't Work

The WinUI 3 XAML compiler has these Windows-only dependencies:

1. **Windows .NET Framework 4.7.2** - Not fully compatible with Wine/Mono
2. **Windows SDK APIs** - Registry, COM, WinRT projections
3. **XAML to XBF compilation** - Windows-specific binary format
4. **Native Windows APIs** - File system,Token usage, DLL loading

Even with Wine + Mono + Xvfb, the XAML compiler fails due to missing Windows APIs.

---

## 🔧 Development Without Building

You can still develop on Linux:

1. **Edit Code**: All C# and XAML files are plain text
2. **Syntax Validation**: Use IDEs like VS Code with C# extensions
3. **Code Review**: Review logic, architecture, patterns
4. **Documentation**: Update README, comments, XML docs
5. **Build on Windows**: Use Option 1, 2, or 3 above

---

## 📦 Pre-built Binaries

If you want to test without building, you can:

1. Ask someone with Windows to build and share the exe
2. Use GitHub Actions (Option 2) to automate builds
3. Use a CI/CD service with Windows runners

---

## 🎯 Next Steps

For this project, I recommend:

1. **Commit all code** ✅ (Already done)
2. **Push to GitHub** 
3. **Set up GitHub Actions** (Option 2) to auto-build on push
4. **Test the downloaded exe** on a Windows machine

This way, you can develop on Linux and automatically get Windows builds!
