# ChapterTool - Modern Cross-Platform Edition

A modern, cross-platform chapter extraction and editing tool built with Avalonia UI and .NET 8.

## Features

- **Cross-Platform**: Runs on Windows, macOS, and Linux
- **Modern UI**: Built with Avalonia UI framework
- **Multiple Format Support**: Extract and edit chapters from various media formats
- **MVVM Architecture**: Clean separation of concerns with proper MVVM pattern

## Supported File Formats

### Input Formats
- **OGM** (`.txt`) - Simple text-based chapter format
- **XML** (`.xml`) - Matroska XML chapter format
- **MPLS** (`.mpls`) - Blu-ray playlist files
- **IFO** (`.ifo`) - DVD information files
- **XPL** (`.xpl`) - HD DVD playlist files
- **CUE** (`.cue`, `.flac`, `.tak`) - Cue sheets and embedded cues
- **Matroska** (`.mkv`, `.mka`) - Matroska media files
- **MP4** (`.mp4`, `.m4a`, `.m4v`) - MP4 media files
- **WebVTT** (`.vtt`) - Web Video Text Tracks

### Export Formats
- Plain text (OGM format)
- XML (Matroska format)
- QPF (QP file for video encoding)
- JSON (custom format)
- CUE sheets
- Timecodes

## Requirements

### Runtime
- .NET 8 Runtime
- **For Matroska support**: [MKVToolNix](https://mkvtoolnix.download/)
- **For MP4 support**: libmp4v2 (included for Windows)

### Development
- .NET 8 SDK
- Visual Studio 2022, VS Code, or Rider

## Building from Source

```bash
# Clone the repository
git clone https://github.com/tautcony/ChapterTool.git
cd ChapterTool

# Restore dependencies
dotnet restore ChapterTool.Modern.sln

# Build the solution
dotnet build ChapterTool.Modern.sln

# Run the application
dotnet run --project ChapterTool.Avalonia
```

## Project Structure

```
ChapterTool/
├── ChapterTool.Core/           # Core business logic library
│   ├── Models/                 # Data models
│   ├── Util/                   # Utilities and helpers
│   ├── ChapterData/            # Format-specific parsers
│   ├── Knuckleball/            # MP4 chapter support
│   └── SharpDvdInfo/           # DVD parsing
├── ChapterTool.Avalonia/       # Avalonia UI application
│   ├── Views/                  # XAML views
│   ├── ViewModels/             # View models
│   └── Assets/                 # Icons and resources
└── Time_Shift/                 # Legacy .NET Framework version
```

## Architecture

The application follows the MVVM (Model-View-ViewModel) pattern:

- **Models** (`ChapterTool.Core`): Platform-independent business logic
- **Views** (`ChapterTool.Avalonia/Views`): Avalonia XAML UI definitions
- **ViewModels** (`ChapterTool.Avalonia/ViewModels`): Presentation logic and data binding

### Key Components

#### Core Library
- **ChapterInfo**: Main data model for chapter information
- **Chapter Parsers**: Format-specific parsers for all supported formats
- **ToolKits**: Utility methods for time conversions and formatting
- **Cross-Platform Services**:
  - `RegistryStorage`: JSON-based settings storage
  - `Logger`: Event-based logging
  - `Notification`: UI notification abstraction

#### Avalonia UI
- **MainWindowViewModel**: Main application view model
- **ChapterViewModel**: Individual chapter data binding
- MVVM commands for file operations

## Platform-Specific Notes

### Windows
- Native MP4 support with bundled libmp4v2.dll
- MKVToolNix detection via installation path

### Linux
- Install libmp4v2 via package manager:
  ```bash
  # Debian/Ubuntu
  sudo apt install libmp4v2-2
  
  # Fedora/RHEL
  sudo dnf install libmp4v2
  
  # Arch Linux
  sudo pacman -S libmp4v2
  ```
- MKVToolNix typically in `/usr/bin`

### macOS
- Install dependencies via Homebrew:
  ```bash
  brew install mp4v2 mkvtoolnix
  ```

## Publishing

### Self-Contained Executable

```bash
# Windows
dotnet publish ChapterTool.Avalonia -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish ChapterTool.Avalonia -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# macOS
dotnet publish ChapterTool.Avalonia -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

### Framework-Dependent

```bash
dotnet publish ChapterTool.Avalonia -c Release -r <runtime-identifier>
```

## Migration from Legacy Version

This modern version maintains compatibility with chapter files created by the legacy .NET Framework version. Settings and configurations will be migrated from Registry (Windows) to JSON-based storage automatically.

For detailed migration information, see [MIGRATION.md](../MIGRATION.md).

## Usage

1. **Load a File**: Click "Load File" and select a media file or chapter file
2. **View Chapters**: Chapters are displayed in the main grid
3. **Edit Chapters**: Modify chapter names and times
4. **Apply Time Expression**: Use expressions to adjust all chapter times
5. **Export**: Choose export format and save chapters

## Development Status

This is the modern cross-platform rewrite of ChapterTool. Current status:

✅ Core library fully functional with all parsers
✅ Avalonia UI framework set up
✅ Basic MVVM architecture implemented
🚧 UI implementation in progress
🚧 Full feature parity with legacy version
🚧 Comprehensive testing on all platforms

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

Distributed under the GPLv3+ License. See [LICENSE](../LICENSE) for more information.

## Acknowledgments

- Original .NET Framework version by TautCony
- [Avalonia UI](https://avaloniaui.net/) - Cross-platform XAML framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM helpers
- All the open-source projects that made this possible

## Links

- **Original Project**: [GitHub](https://github.com/tautcony/ChapterTool)
- **Documentation**: [Wiki](https://github.com/tautcony/ChapterTool/wiki)
- **Issue Tracker**: [Issues](https://github.com/tautcony/ChapterTool/issues)
