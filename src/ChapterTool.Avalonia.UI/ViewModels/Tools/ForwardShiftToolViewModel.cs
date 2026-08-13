using ChapterTool.Avalonia.UI.PlatformPorts.SessionPorts;

namespace ChapterTool.Avalonia.UI.ViewModels.Tools;

public sealed class ForwardShiftToolViewModel : ObservableViewModel
{
    public ForwardShiftToolViewModel(IChapterEditPort chapterEdit, Func<Exception, ValueTask>? errorHandler = null)
    {
        ApplyCommand = new UiCommand((parameter, _) =>
        {
            if (parameter is ForwardShiftToolViewModel viewModel)
            {
                chapterEdit.ShiftFramesForward((int)viewModel.Frames);
            }

            return ValueTask.CompletedTask;
        })
        {
            ErrorHandler = errorHandler
        };
    }

    public decimal Frames
    {
        get;
        set => SetProperty(ref field, value);
    }

    public UiCommand ApplyCommand { get; }
}
