# Release Notes

## [23.3.2] - 2026.08.29

### Features

- Added media track information processing and chapter import projection to improve recognition and display of multi-track media.
- Added frame display and chapter deletion timing settings so editing and time display behavior can be adjusted to user preferences.
- Added selection indicators and a context menu to ClipBox.

### Bug Fixes

- Improved cross-platform BDMV paths and imported filename formatting for more consistent imports across operating systems.
- Fixed time information being lost after deleting a chapter row.
- Improved DataGrid header and separator styles, and fixed the Advanced Options layout at different window widths and languages.
- Improved language display names and localization resources, aligned Chinese, English, and Japanese text, and standardized the `mkvextract` tool name.

### Internals

- Improved desktop publishing configuration and the GitHub Release trigger workflow, including release artifact validation.
- Integrated Python utility scripts with the uv environment and Ruff checks.
- Expanded unit and Headless test coverage for Core, Infrastructure, CLI, and Avalonia to improve release verification reliability.

## [23.3.1] - 2026.08.13

### Features

- Completed native BDMV import: parse INDEX, CLPI, BDJO, and Movie Object files, discover titles from complete playlists, and merge display names in the eac3to style.

### Bug Fixes

- Aligned HDMV offsets and CLPI boundaries with libbluray 1.5.1, and hardened importer, time formatting, and localization handling for invalid input.
- Unified the Avalonia visual design system, improved load entry points and shortcut hints, and removed the Save As entry point.
- Improved log aggregation and readability.

### Internals

- Simplified Avalonia composition across hosts and used Autofac to manage service lifetimes on desktop.
- Improved TypeScript types, tests, and documentation for the Node.js package.

## [23.3.0] - 2026.07.30

### Features

- Extended portable import paths: embedded CUE files in FLAC/TAK are now handled through `ChapterContentService`, allowing direct reads in environments without external tools such as WASM and Node.
- Added Chinese, English, and Japanese error and diagnostic localization to the CLI. Moved desktop strings to axaml resources and improved ready-state text and empty output path display when switching languages.

### Bug Fixes

- Adjusted the browser workspace Advanced Options layout to match the desktop workflow and improved Chinese, English, and Japanese UI localization with JSON resources.
- Improved expression tool editing with compact mode and completion anchored to the caret line, and switched to system fonts and FontAwesome icons.

### Internals

- Split desktop and command-line hosts: `ChapterTool.CommandLine` is now an independent CLI executable and .NET Tool, while the desktop Avalonia host only starts the graphical interface and no longer dispatches CLI arguments.
- Extracted shared desktop UI into `ChapterTool.Avalonia.UI` (main view, tool windows, workflows, and ViewModels), and moved settings and platform contracts into `ChapterTool.Contracts`.
- Moved workspace session models such as `ChapterWorkspace` and `ClipSession` into `ChapterTool.Core` for use by desktop, browser, and command-line hosts.

## [23.2.1] - 2026.07.22

### Features

- Added an independent command-line program and .NET Tool package, and extracted the command-line workflow into shared components.
- Extended command-line chapter conversion with Lua expressions, built-in presets, and runtime importer composition.
- Upgraded the browser sample to a complete Blazor WebAssembly workspace.
- Added a .NET WebAssembly-based Node.js API and npm package for chapter import, conversion, validation, and export.

### Bug Fixes

- Added localization, settings, and workflow validation to the browser workspace.

### Internals

- Improved the NuGet, npm, and GitHub Release publishing pipelines, with consistent version and artifact validation.
- Updated project dependencies and code navigation documentation.

## [23.2.0] - 2026.07.18

### Features

- Completed the settings system with versioned settings documents, aggregated persistence, theme presets, independent fonts, save directories, output encoding, and frame rate settings.
- Enhanced the expression editing workflow with script loading, multiline editing, validation, chapter context, and more complete diagnostic feedback.
- Added a `ChapterTool.Core` browser WebAssembly sample and made it available through GitHub Pages.

### Bug Fixes

- Unified chapter load, save, and export factories for the CLI and GUI, and improved option binding and multi-segment chapter name generation.

### Internals

- Split main-window session state and introduced typed ClipSession, ChapterWorkspace, tool ports, and a window registry to clarify workspace state ownership.
- Strengthened GitHub Pages builds and WASM release artifact validation.

## [23.1.0] - 2026.07.09

### Features

- Introduced a new chapter expression engine and Lua implementation with time, frame rate, and chapter index context, plus a script execution budget.

### Bug Fixes

- Standardized chapter time properties as `StartTime` and `EndTime`, abstracted the media chapter reading boundary, and improved media container importer replaceability.
- Strengthened validation and failure diagnostics at the settings file, external importer, core API, and CLI startup boundaries. Corrupt settings are preserved and unsafe process argument handling is avoided.

### Internals

- Prepared the `ChapterTool.Core` NuGet package for .NET 8, .NET 9, and .NET 10, including package README, license, documentation, and symbol package configuration.
- Refactored chapter import, editing, and export models with structured import progress, diagnostic codes, diagnostic sources, and unified import/export format descriptions.
- Added Sentry startup diagnostics, used source-generated JSON serialization to improve runtime performance, and reorganized Avalonia Headless tests.
- Improved cross-platform CI, release scripts, and application publishing configuration, and removed deprecated installer assets.

## [23.0.0] - 2026.07.07

### Features

- Added the official command-line entry point with `formats`, `inspect`, `convert`, and `load` commands.
- Enhanced expression editing with syntax highlighting, completion, and diagnostics.
- Extended and organized import and export support for text, XML, WebVTT, CUE, MPLS, BDMV, IFO, XPL, Matroska, and common media containers. Export supports TXT, XML, QPF, TimeCodes, tsMuxeR meta, CUE, JSON, WebVTT, Celltimes, and Chapter2QPF.

### Bug Fixes

- Improved external tool discovery and ffprobe/mkvextract/eac3to diagnostics and fallback paths for more stable messages when files have no chapters, are invalid, or contain multiple editions, clips, or titles.

### Internals

- Migrated to .NET 10 and a cross-platform Avalonia desktop architecture while retaining core chapter loading, editing, combining, preview, and export workflows.

## [2.33.33.3331] - 2023.01.21

### Features

- Added handling for MPLS files whose play items have no marks.

### Bug Fixes

- Removed registry usage.

### Internals

- Updated dependencies.
- Added a GitHub Action.

## [2.33.33.333] - 2022.09.27

### Bug Fixes

- Fixed thread invocation when updating the chapter list.

## [2.33.33.332] - 2021.01.07

### Features

- Added an automatic version update script.

### Bug Fixes

- Fixed the x64 build target.

### Internals

- Updated dependencies.
- Organized code style.
- Switched JSON output to serialization.

## [2.33.33.331] - 2020.08.30

### Features

- Added an installer build script.

### Bug Fixes

- Fixed exceptions during some time edits.
- Fixed the `Any CPU` build condition that prevented MP4 files from loading correctly on 64-bit systems.
- Fixed time not carrying to the next second when rounding produced 1000 milliseconds.

## [2.33.33.33] - 2019.03.23

### Features

- Added time and frame count editing.

### Internals

- Continued work on new features, bug fixes, and performance improvements.

## [2.33.33.32] - 2018.07.23

### Features

- Improved DVD chapter reading.

### Bug Fixes

- Exported QPF files without a BOM.

## [2.33.33.31] - 2018.02.25

### Features

- Added the English interface.

### Bug Fixes

- Fixed some high-DPI issues.
- Fixed DVD chapter reading.
- General bug fixes and performance improvements.

## [2.33.33.3] - 2017.05.07

### Features

- Added full-featured UHD chapter reading.
- Added a new JetBrains-style icon.

### Internals

- General bug fixes and performance improvements.

## [2.33.33.2] - 2017.05.04

### Features

- Added JSON output.
- Added basic UHD Blu-ray support.

### Bug Fixes

- Fixed reading embedded CUE files.
- Added high-DPI support.
- General bug fixes and performance improvements.

## [2.33.33.1] - 2017.01.22

### Features

- Added append support for MPLS.
- Added an update prompt for older .NET runtimes.
- Added a new FLAC parser to provide more FLAC-related information (mainly for ATI).

### Internals

- General bug fixes and performance improvements.

## [2.33.32.33] - 2016.09.10

### Features

- Added expressions for controlling timestamp conversion. All operators use left associativity.
- Added infix and reverse Polish expression forms, limited to operators supported by VapourSynth.

### Internals

- Logged the converted reverse Polish or infix expression, as requested by QPet.

## [2.33.32.32] - 2016.08.13

### Features

- Added WebVTT support.
- Added a feature to shift forward by a specified number of frames, as requested by wongyi.
- Added support for BDMV folders. The parent folder containing BDMV must contain only ASCII characters.

### Bug Fixes

- Fixed automatically generated chapter names when files were loaded consecutively.
- Fixed row background colors.

## [2.33.32.31] - 2016.06.24

### Features

- Double-clicking the frame count column now copies frame information to the clipboard.
- Added automatic multiplication by 1.001 for IFO chapters with a 29.970 frame rate.

### Bug Fixes

- Fixed cross-thread access to the update-window progress bar.
- Fixed Alt+S not saving the last chapter.

## [2.33.32.3] - 2016.05.17

### Features

- Added the `--zones` argument.
- Moved the progress bar and related indicators to the status strip.
- Added several hotkeys.

### Bug Fixes

- Fixed saving when no language was selected.
- Fixed frame rate display.
- Removed `-1` from qpfile output.

## [2.33.32.2] - 2016.05.05

### Features

- Added MP4 support.
- Added CUE export.
- Added a context-menu reload command for the Load button.

### Bug Fixes

- Fixed crashes during frame recognition at specific frame rates.
- Fixed errors when loading empty MPLS files.

## [2.33.32.1] - 2016.04.02

### Features

- Added detailed MPLS information display.
- Added an online upgrade page on GitHub.

### Bug Fixes

- Fixed the duration of merged MPLS chapters.
- Fixed MPLS frame rates for 50p and 60i.
- Fixed chapter timestamps after chapter deletion.

## [2.33.31.3] - 2016.03.15

### Features

- Improved online upgrades.
- Improved error messages.

### Bug Fixes

- Fixed missing video filenames when saving MPLS chapter files.
- Fixed the UI not updating when two segments had the same number of chapter timestamps.
- Fixed exceptions in some cases.
- Fixed the first chapter not starting at 00:00:00.000 when leading black video existed in an MPLS chapter.

## [2.33.31.2] - 2016.03.09

### Features

- Added context-menu commands to open the corresponding MPLS and IFO files.
- Added HDDVD chapter (`.xpl`) support.

### Bug Fixes

- Fixed CUE parsing.

## [2.33.31.1] - 2016.02.29

### Features

- Added online updates.

### Bug Fixes

- Fixed the color settings file path in some cases.
- Rewrote text chapter parsing for improved stability.
- Improved frame count rounding details.
- Fixed the 1.001 multiplier after switching clips.

### Internals

- Added hints for some options.

## [2.33.3.3] - 2016.02.16

### Features

- Added limited support for child chapters in XML chapter files.
- Added CUE support, including embedded CUE in TAK and FLAC.

### Bug Fixes

- Fixed XML chapter parsing.
- Fixed extra spaces in some language options in XML save settings.
- When an MPLS or IFO chapter had no loaded chapter-name template, deleting a row now restarts numbering at Chapter 01.

### Internals

- Improved perceived list insertion smoothness.
- Fixed many details.

## [2.33.3.2] - 2016.01.14

### Bug Fixes

- Fixed the IFO chapter merge function.
- Fixed loaded file format matching.
- Fixed the selection box remaining unselected after loading XML chapters.
- Fixed missing prompts after loading XML or MKV chapters.

## [2.33.3.1] - 2015.12.20

### Features

- Added display of all corresponding video files for chapter segments with multiple angles.
- Added saving for Time Codes and tsMuxeR Meta information.
- Added IFO chapter merging as an experimental feature.
- Added an option to open `.mpls` files with ChapterTool from the context menu. Administrator privileges are required.

### Bug Fixes

- Fixed precision in time correction after extracting IFO chapters; the operation is now manual.
- Fixed duplicate errors after a valid chapter file was followed by an invalid OGM chapter file.
- Fixed a crash when hovering over Save after loading an invalid MPLS file.
- Fixed crashes after clicking Refresh or Preview after loading an invalid file.
- Fixed the QPF output format and duplicate `\r` after OGM chapter names.
- Fixed lag caused by repeatedly clicking the expanded-window button.
- Fixed IFO chapter segment filename display.
- Fixed all loaded IFO chapter numbers being zero.

### Internals

- Added color to the progress bar.

## [2.33.2.3] - 2015.12.05

### Features

- Added the version to the title bar.
- Prefer installed MkvToolnix when available.
- Added invalid-file prompts.

### Bug Fixes

- Fixed frame rate reading for MPLS files containing MPEG-II or VC-1 video.
- The interface now shows only the loaded filename instead of the full path.
- Fixed the XML output option "Do not use chapter names".
- Fixed time shifting.
- Fixed frame count calculation after time shifting.

### Internals

- Improved robustness.
- Added save-information logging.

## [2.33.2.2] - 2015.11.18

### Features

- Added saving for interface color settings.

### Bug Fixes

- Fixed ordered chapter reading.

## [2.33.2.1]

### Features

- Added XML chapter parsing with support for multi-chapter XML input.
- Added a language option for XML chapter output.

### Bug Fixes

- Fixed loading XML after MPLS.
- Added a scroll bar for long previews.
- Fixed frame count calculation for 25 fps IFO files.
- Improved display performance.
- Progress now reaches one-third for segments without chapters.

### Internals

- Updated the bundled mkvextract to 8.5.1.

## [2.33.1.3]

### Bug Fixes

- Fixed PlayList Mark parsing when Entry Mark and Link Point entries appeared in an alternating order. This regression was introduced in 2.3.331.1.

## [2.33.1.2] - 2015.10.09

### Features

- Added blue and white alternating rows to the list.
- Updated the tutorial.

### Bug Fixes

- Improved list refresh efficiency and reduced flicker.
- Fixed some IFO reading issues.

## [2.33.1.1]

### Features

- Enabled a new, clearer, and simpler interface.
- Improved chapter-name templates so they remain active after selection.
- Added TXT, XML, and QPF output format options.

## [2.3.333.2.fix]

### Bug Fixes

- Fixed the file dialog filter.

## [2.3.333.2] - 2015.09.14

### Features

- Added full MPLS chapter output.
- Added IFO file support.
- Added Alt+A to automatically select the latter half of the text box.

### Bug Fixes

- Fixed combo-box background colors.
- Improved loaded file path display.
- Fixed excessive decimal places when loading MKV chapters.

## [2.3.333.1] - 2015.09.13

### Features

- Updated the tutorial with new chapter-splitting steps.
- Began using Git.

### Bug Fixes

- Fixed incorrect window sizing.
- Improved calculation precision.
- Fixed many details.
- Fixed loading files with spaces by dragging them to the icon, a regression since 2.3.31.1.

## [2.3.332.3]

### Bug Fixes

- Fixed an error when opening the color settings window for the second time.
- For incorrectly muxed MKV files containing two chapters, only the first is read.
- Fixed the time-shift option not updating unless it was reselected.
- Fixed the 1 ms error caused by `double` values since 2.3.332.1.
- Rounded the millisecond portion of times read from MPLS.

### Internals

- Replaced `DateTime` variables with `TimeSpan` variables.

## [2.3.332.2]

### Features

- Added window position persistence after closing the program.

### Bug Fixes

- Fixed multiple Log, color settings, or About windows being opened.
- Fixed errors when loading MKV files without chapters and converting another chapter file afterward.
- Disabled editing in the right text box.

### Internals

- Expanded log contents.

## [2.3.332.1]

### Features

- Added application runtime logging.

### Internals

- Replaced MediaInfo with MKVextract.
- Fixed automatic frame rate detection.

## [2.3.331.1]

### Features

- Added clickable hints in the frame count calculation area.
- Improved frame count calculation details.
- Added detailed hints.
- Added more detailed prompts for segments containing only two chapter points.

### Bug Fixes

- Fixed several bugs.
- Fixed the segment selector disappearing when an MPLS file was dragged to the icon.

### Internals

- Added new meaning to the "Something happened" message.
- Removed the old "mystical programming" phrase.

## [2.3.33.4]

### Bug Fixes

- Fixed a crash when hovering over Save without an MPLS file loaded.

## [2.3.33.3]

### Features

- Added custom interface colors. These settings are not persisted and are opened from the Convert button context menu.
- Added a prompt asking whether a two-point MPLS segment is a pseudo-chapter when saving.

### Bug Fixes

- Fixed a difficult-to-find bug through VS2015.
- Fixed QPF duplicate-name detection.
- Switched back to the light theme.
- Added a prompt for excessively long paths.
- Fixed a black box on the first click of More.

## [2.3.33.2]

### Features

- Remembered the manually selected save path.

### Bug Fixes

- Added a prompt when `mediaInfo.dll` could not be loaded.

## [2.3.33.1]

### Features

- Added limited Matroska support.
- Updated the tutorial to v3.

### Bug Fixes

- Fixed a crash when the input was an empty or whitespace-only file.

### Internals

- Refreshed the visual appearance.

## [2.3.32.1]

### Bug Fixes

- Rewrote MPLS parsing to fix mismatches between some chapters and filenames.
- Fixed a data consistency issue.
- Reduced the number of try-catch blocks through improved regular-expression handling.

## [2.3.31.1]

### Features

- Added drag-and-drop loading from the application icon.
- Added a Save button context menu for setting the save path.
- Updated the tutorial to v2.

### Bug Fixes

- Fixed loading read-only MPLS files.

## [2.3.3.333]

### Features

- Added a "Something happened" dialog as a Windows 10 launch commemorative feature.

### Bug Fixes

- Fixed missing first digits in filenames inside MPLS files.

## [2.3.3.332]

### Features

- Added drag-and-drop support to the About window.
- Added manual adjustment of the frame rounding error range.
- Added limited QPF support.
- Added the first version of the tutorial.

### Bug Fixes

- Fixed values such as 1.001 frames being displayed as 2 frames.
- MPLS chapters are now displayed even when only one video exists.
- Fixed crashes when loading invalid MPLS files.

## [2.3.3.331]

### Bug Fixes

- Fixed the black box shown during resizing.
- Fixed hidden controls still being reachable with Tab.
- Fixed crashes when loading XML files that were not chapter files.

### Internals

- Aligned controls.

## [2.3.3.33]

### Features

- Added limited MPLS support.
- Added more selectable frame rates.

### Internals

- Temporarily canceled the multilingual interface plan.
- Redrew the logo and reduced its size by 13 KiB.

## [2.3.3.32]

### Bug Fixes

- Improved AUTO calculation to avoid incorrect output.

## [2.3.3.31]

### Features

- Added the English interface.

### Internals

- Used mask recognition for shift time.

## [2.3.3.3]

### Features

- Added limited XML chapter support.

### Bug Fixes

- Fixed several bugs.
- Fixed path prompts when loading files with the Load button.
- Changed output extension generation to avoid overwriting files.

### Internals

- Changed the About page display.
- Removed the unused Save button.

## [2.3.3.2]

### Features

- Added automatic conversion while loading files.
- Added a 1.001 multiplier to all chapter start times for chapters extracted with DVD Decrypter.
- Added shifting for all chapter numbers and timestamps, such as 00:23:23.233.
- Added chapter-name template selection.

## [2.3.3.1]

### Features

- Added an automatic frame rate detection button.

### Bug Fixes

- Fixed frame counts being incremented by one when already integers.

## [2.3.3.0]

### Internals

- Merged the TimeCal code.

## [2.3.2.3]

### Features

- Added a root progress bar.

### Bug Fixes

- Fixed several bugs.

### Internals

- Removed TextBox borders.

## [2.3.2.2]

### Bug Fixes

- Fixed several bugs.

### Internals

- Improved prompt and button sizes.
- Changed the target framework to .NET 3.5.

## [2.3.2.1]

### Bug Fixes

- Fixed several bugs.

## [2.3.2.0]

### Features

- Added a button to save the source file.

## [2.3.1.0]

### Features

- Added the Chinese interface.

## [2.3.0.0]

### Internals

- Rewrote Convert button logic to read content from the TextBox instead of directly from the file.

## [2.2.0.0]

### Features

- Added an icon.
- Added support for text content containing blank lines.

### Bug Fixes

- Fixed several bugs.

## [2.1.0.0] - 2015.05.08

### Features

- Added an option to preserve original chapter names.

----------------------------

## [2.0.0.0]

### Internals

- First usable C# version.

## [1.7]

### Features

- Added automatic removal of leading blank lines.

## [1.6]

### Bug Fixes

- Fixed issues caused by file extensions.

### Internals

- Unified code style.
- Compiled in VS2013 Release mode to reduce program size.
- Changed output file path generation.
- Added a console title.

## [1.5]

### Features

- Removed the file processing limit.

### Internals

- Changed output filenames to the original filename plus `_`.

## [1.4]

### Bug Fixes

- Fixed the algorithm to prevent incorrect output in some special cases.

## [1.3]

### Features

- Added prompts when the file processing limit is exceeded or the program is opened directly.

## [1.2]

### Features

- Added multi-file processing.

### Internals

- Removed the chapter-name preservation option.

## [1.1]

### Internals

- Not remembered.

## [1.0]

### Internals

- First C++ version.
