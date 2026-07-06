using ChapterTool.Avalonia.ViewModels;

namespace ChapterTool.Avalonia.Tests.Commands;

public sealed class UiCommandTests
{
    [Fact]
    public async Task ExecuteAsyncTracksBusyStateAndRaisesAvailability()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new UiCommand(async (_, _) => await release.Task);
        var availabilityChanges = 0;
        command.CanExecuteChanged += (_, _) => availabilityChanges++;

        var execution = command.ExecuteAsync().AsTask();

        Assert.True(command.IsExecuting);
        Assert.False(command.CanExecute());
        release.SetResult();
        await execution;

        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute());
        Assert.True(availabilityChanges >= 2);
    }

    [Fact]
    public async Task ExecuteAsyncExposesObservedExceptions()
    {
        var expected = new InvalidOperationException("boom");
        var command = new UiCommand((_, _) => throw expected);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () => await command.ExecuteAsync().AsTask());

        Assert.Same(expected, actual);
        Assert.Same(expected, command.ExecutionError);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task ExecuteObservesExceptionsWithoutThrowingToCaller()
    {
        var expected = new InvalidOperationException("boom");
        var command = new UiCommand((_, _) => throw expected);
        var observedError = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        command.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UiCommand.ExecutionError))
            {
                observedError.TrySetResult(command.ExecutionError);
            }
        };

        command.Execute(null);
        var actual = await observedError.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Same(expected, actual);
    }
}
