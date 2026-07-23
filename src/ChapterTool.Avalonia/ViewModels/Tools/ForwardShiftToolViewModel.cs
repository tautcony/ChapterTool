using ChapterTool.Avalonia.Session.Ports;

namespace ChapterTool.Avalonia.ViewModels.Tools;

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
