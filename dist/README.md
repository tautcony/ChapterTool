# Distribution

ChapterTool's maintained distribution path is the .NET 10 Avalonia publish flow:

- `scripts/publish.sh`
- `scripts/publish.ps1`
- `.github/workflows/dotnet-ci.yml`

The legacy Windows NSIS installer inputs were retired because they targeted the old
`Time_Shift`/`ChapterTool.exe` layout and carried an independent hard-coded version.
Any future installer must consume the current `src/ChapterTool.Avalonia` publish
output and derive metadata from the MSBuild version source in `Directory.Build.props`.
