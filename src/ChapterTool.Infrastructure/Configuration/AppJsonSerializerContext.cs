using System.Text.Json.Serialization;
using ChapterTool.Infrastructure.Importing.Media;

namespace ChapterTool.Infrastructure.Configuration;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ThemeSettings))]
[JsonSerializable(typeof(FfprobeChapterOutput))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
