using System.Text.Json.Serialization;
using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Infrastructure.Configuration;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(FontSettings))]
[JsonSerializable(typeof(ThemeSettings))]
[JsonSerializable(typeof(ChapterToolSettings))]
[JsonSerializable(typeof(FfprobeChapterOutput))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
