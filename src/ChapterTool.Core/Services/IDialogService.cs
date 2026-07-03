namespace ChapterTool.Core.Services;

public interface IDialogService
{
    ValueTask<DialogResult> ShowMessageAsync(DialogRequest request, CancellationToken cancellationToken);
}
