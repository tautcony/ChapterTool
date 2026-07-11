using System.Collections.ObjectModel;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Threading;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.Session.Ports;
using ChapterTool.Core.Exporting;
using ChapterTool.Infrastructure.Services;

namespace ChapterTool.Avalonia.ViewModels;

public sealed class ForwardShiftToolViewModel(IChapterEditPort chapterEdit) : ObservableViewModel
{
    public decimal Frames
    {
        get;
        set => SetProperty(ref field, value);
    }

    public UiCommand ApplyCommand { get; } = new((parameter, _) =>
    {
        if (parameter is ForwardShiftToolViewModel viewModel)
        {
            chapterEdit.ShiftFramesForward((int)viewModel.Frames);
        }

        return ValueTask.CompletedTask;
    });
}

