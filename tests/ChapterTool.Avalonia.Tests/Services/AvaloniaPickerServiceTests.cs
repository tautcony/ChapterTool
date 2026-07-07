using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Services;

namespace ChapterTool.Avalonia.Tests.Services;

public sealed class AvaloniaPickerServiceTests
{
    [Fact]
    public void File_picker_options_use_localized_titles_and_file_type_names()
    {
        var localizer = new AppLocalizationManager("zh-CN");

        var source = AvaloniaFilePickerService.CreateSourceOptions(localizer);
        var mpls = AvaloniaFilePickerService.CreateMplsOptions(localizer);
        var template = AvaloniaFilePickerService.CreateChapterNameTemplateOptions(localizer);
        var luaScript = AvaloniaFilePickerService.CreateLuaExpressionScriptOptions(localizer);
        var saveDirectory = AvaloniaFilePickerService.CreateSaveDirectoryOptions(localizer);
        var executable = AvaloniaSettingsPickerService.CreateExecutableOptions("选择工具", localizer);

        Assert.Equal("打开源文件", source.Title);
        Assert.Equal("章节和媒体文件", source.FileTypeFilter?.First().Name);
        Assert.Equal("追加 MPLS", mpls.Title);
        Assert.Equal("MPLS 播放列表", mpls.FileTypeFilter?.First().Name);
        Assert.Equal("打开章节名称模板", template.Title);
        Assert.Equal("文本文件", template.FileTypeFilter?.First().Name);
        Assert.Equal("打开 Lua 表达式脚本", luaScript.Title);
        Assert.Equal("Lua 脚本文件", luaScript.FileTypeFilter?.First().Name);
        Assert.Equal("保存章节到", saveDirectory.Title);
        Assert.Equal("选择工具", executable.Title);
        Assert.Equal("可执行文件", executable.FileTypeFilter?.First().Name);
    }
}
