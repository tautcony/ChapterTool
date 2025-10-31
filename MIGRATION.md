# ChapterTool Avalonia Migration Guide

## Overview

This document outlines the migration of ChapterTool from .NET Framework 4.8 WinForms to .NET 8 with Avalonia UI, enabling true cross-platform support (Windows, macOS, Linux).

## Architecture

The migration follows a clean MVVM architecture with clear separation of concerns:

```
ChapterTool/
├── ChapterTool.Core/              # Platform-independent business logic (.NET 8)
│   ├── Models/                    # Data models
│   ├── Util/                      # Utilities and helpers
│   ├── ChapterData/               # Chapter format parsers
│   ├── Knuckleball/               # MP4 chapter support
│   └── SharpDvdInfo/              # DVD info extraction
├── ChapterTool.Avalonia/          # Avalonia UI application (.NET 8)
│   ├── Views/                     # XAML views
│   ├── ViewModels/                # View models (MVVM pattern)
│   ├── Assets/                    # Icons, images
│   └── Services/                  # Platform-specific services
└── Time_Shift/                    # Legacy WinForms application (.NET Framework 4.8)
```

## Migration Status

### Completed ✅
- Created Avalonia MVVM project structure with .NET 8
- Created ChapterTool.Core library for business logic
- Migrated platform-independent utility classes:
  - Chapter, ChapterName, ChapterInfoGroup
  - Expression evaluator
  - Logger
  - All chapter format parsers (BDMV, CUE, IFO, Matroska, MP4, MPLS, OGM, VTT, XML, XPL)
- Replaced Jil JSON library with System.Text.Json
- Created platform-independent ChapterInfo model
- Set up SDK-style project files

### In Progress 🔄
- Abstracting platform-specific dependencies
- Fixing remaining build errors in Core library
- Creating cross-platform abstractions for:
  - Settings storage (Registry → cross-platform)
  - Native library loading (libmp4v2)
  - File dialogs and notifications

### Todo 📋
1. **Complete Core Library**
   - Fix remaining compilation errors
   - Add missing extension methods (GetUTFString)
   - Create cross-platform abstractions:
     - `ISettingsService` for Registry replacement
     - `IDialogService` for file/folder dialogs
     - `INotificationService` for user notifications
     - `ILanguageService` for language selection

2. **Create Avalonia UI**
   - Main Window (Form1 replacement)
     - Chapter list DataGrid
     - File loading controls
     - Export format selection
     - Time expression input
   - About Dialog (FormAbout replacement)
   - Color Picker Dialog (FormColor replacement)
   - Log Viewer (FormLog replacement)
   - Preview Window (FormPreview replacement)
   - Updater Dialog (FormUpdater replacement)

3. **Implement ViewModels**
   - MainWindowViewModel
     - Chapter list management
     - File operations
     - Export functionality
     - Time calculations
   - Shared command implementations
   - Data validation

4. **Resource Migration**
   - Copy and adapt icons
   - Implement localization (English/Chinese)
   - Style definitions

5. **Native Library Support**
   - Cross-platform libmp4v2 loading
   - Platform-specific P/Invoke handling
   - Bundle native libraries for each platform

6. **Testing**
   - Migrate existing unit tests
   - Add integration tests for UI
   - Test on Windows, Linux, macOS

## Key Technical Changes

### JSON Serialization
**Before (Jil):**
```csharp
[JilDirective(Name="name")]
public string Name { get; set; }

var json = Jil.JSON.Serialize(obj);
```

**After (System.Text.Json):**
```csharp
[JsonPropertyName("name")]
public string Name { get; set; }

var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
```

### Settings Storage
**Before (Registry):**
```csharp
var value = RegistryStorage.Load(name: "setting");
RegistryStorage.Save("value", name: "setting");
```

**After (Cross-platform):**
```csharp
// Create ISettingsService implementation
public interface ISettingsService
{
    string? Load(string key);
    void Save(string key, string value);
}

// Use JSON file or platform-specific storage
var value = settingsService.Load("setting");
settingsService.Save("setting", "value");
```

### File Dialogs
**Before (WinForms):**
```csharp
var dialog = new OpenFileDialog();
if (dialog.ShowDialog() == DialogResult.OK)
{
    var file = dialog.FileName;
}
```

**After (Avalonia):**
```csharp
var dialog = new OpenFileDialog();
var files = await dialog.ShowAsync(window);
if (files != null && files.Length > 0)
{
    var file = files[0];
}
```

### Data Binding (DataGridView → DataGrid)
**Before (WinForms):**
```csharp
dataGridView1.Rows.Add(row);
```

**After (Avalonia):**
```csharp
// Use ObservableCollection in ViewModel
public ObservableCollection<ChapterViewModel> Chapters { get; } = new();

// XAML
<DataGrid ItemsSource="{Binding Chapters}">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Number" Binding="{Binding Number}" />
        <DataGridTextColumn Header="Time" Binding="{Binding TimeString}" />
        <DataGridTextColumn Header="Name" Binding="{Binding Name}" />
    </DataGrid.Columns>
</DataGrid>
```

## Dependencies

### Core Library
- **Removed:** Jil, Costura.Fody, System.Windows.Forms, System.Drawing
- **Added:** System.Text.Json (8.0.5)
- **Retained:** Native interop for libmp4v2

### Avalonia Application
```xml
<PackageReference Include="Avalonia" Version="11.3.6" />
<PackageReference Include="Avalonia.Desktop" Version="11.3.6" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.6" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="11.3.6" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.1" />
```

## Building and Running

### Prerequisites
- .NET 8 SDK
- MKVToolNix (for Matroska support)
- libmp4v2 (for MP4 support)

### Build Commands
```bash
# Restore and build Core library
cd ChapterTool.Core
dotnet restore
dotnet build

# Build and run Avalonia application
cd ../ChapterTool.Avalonia
dotnet restore
dotnet build
dotnet run
```

### Platform-Specific Notes

#### Windows
- Native libraries: libmp4v2.dll (x86/x64)
- MKVToolNix installation path detection

#### Linux
- Install libmp4v2 via package manager
- MKVToolNix via package manager

#### macOS
- Install dependencies via Homebrew
- Handle code signing for distribution

## Remaining Issues to Resolve

1. **GetUTFString Extension Method**
   - Used in CueData.cs and BDMVData.cs
   - Need to implement or find source

2. **Index Type Ambiguity**
   - Conflict between `System.Index` and `Cue.Types.Index`
   - Fixed in some places, need to complete

3. **Platform-Specific Code**
   - Registry access in MatroskaData.cs needs abstraction
   - Native library loading needs cross-platform support

4. **Missing Services**
   - LanguageSelectionContainer (language code mappings)
   - RegistryStorage (settings persistence)
   - Notification (user messages)

## Testing Strategy

1. **Unit Tests** - Test business logic in Core library
2. **Integration Tests** - Test file parsing with sample files
3. **UI Tests** - Test Avalonia views with Avalonia.Headless
4. **Manual Testing** - Test on each target platform

## Deployment

### Single-File Executable
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

### Platform-Specific Packaging
- **Windows:** Create installer with Inno Setup or WiX
- **Linux:** Create AppImage, Flatpak, or Snap package
- **macOS:** Create .app bundle and DMG

## Resources

- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [MVVM Pattern](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/)
- [.NET 8 Migration Guide](https://learn.microsoft.com/en-us/dotnet/core/porting/)
- [Avalonia Samples](https://github.com/AvaloniaUI/Avalonia.Samples)

## Next Steps

1. Complete the Core library compilation
2. Implement required service interfaces
3. Create the main window UI in Avalonia
4. Implement ViewModels with proper data binding
5. Test file loading and chapter parsing
6. Implement export functionality
7. Add localization support
8. Create build and deployment scripts
9. Test on all target platforms
10. Update documentation and README
