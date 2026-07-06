using ChapterTool.Core.Services;

namespace ChapterTool.Infrastructure.Platform;

internal sealed class ScriptedDialogService(params DialogResult[] results) : IDialogService
{
    private readonly Queue<DialogResult> results = new(results);

    public IReadOnlyList<DialogRequest> Requests => requests;

    private readonly List<DialogRequest> requests = [];

    public ValueTask<DialogResult> ShowMessageAsync(DialogRequest request, CancellationToken cancellationToken)
    {
        requests.Add(request);
        return ValueTask.FromResult(results.Count > 0 ? results.Dequeue() : new DialogResult(false));
    }
}
