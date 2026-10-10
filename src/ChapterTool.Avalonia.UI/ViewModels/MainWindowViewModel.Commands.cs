using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.ViewModels;

/// <summary>Contains command wiring for the main window.</summary>
public sealed partial class MainWindowViewModel
{
    private void InitializeCommands()
    {
        InitializeFileCommands();
        InitializeEditCommands();
        InitializeWindowCommands();
    }

    private IEnumerable<UiCommand> AllCommands()
    {
        yield return LoadCommand;
        yield return ReloadCommand;
        yield return AppendMplsCommand;
        yield return DropPathLoadCommand;
        yield return SaveCommand;
        yield return RefreshCommand;
        yield return ChangeFpsCommand;
        yield return SelectClipCommand;
        yield return CombineCommand;
        yield return EditTimeCommand;
        yield return EditNameCommand;
        yield return EditFrameCommand;
        yield return DeleteCommand;
        yield return InsertCommand;
        yield return OpenRelatedMediaCommand;
        yield return PreviewCommand;
        yield return LogCommand;
        yield return SettingsCommand;
        yield return LanguageCommand;
        yield return ExpressionCommand;
        yield return TemplateNamesCommand;
        yield return ZonesCommand;
        yield return ForwardShiftCommand;
        yield return HistoryCommand;
        yield return PreviewContentOptionsCommand;
        yield return ApplyContentPreviewCommand;
        yield return RefreshContentPreviewCommand;
        yield return CancelContentPreviewCommand;
        yield return UndoCommand;
        yield return RedoCommand;
        yield return NavigateHistoryCommand;
    }

    private void InitializeFileCommands()
    {
        LoadCommand = new UiCommand(async (parameter, token) =>
        {
            switch (parameter)
            {
                case ChapterSourceDocument source:
                    await LoadSourceAsync(source, token);
                    break;
                case string path:
                    await LoadPathAsync(path, token);
                    break;
            }
        });
        ReloadCommand = new UiCommand(async (_, token) =>
        {
            if (Workspace.CurrentSource is not null)
            {
                await LoadSourceAsync(Workspace.CurrentSource, token);
            }
        }, _ => Workspace.CurrentSource is not null);
        AppendMplsCommand = new UiCommand(async (parameter, token) =>
        {
            switch (parameter)
            {
                case ChapterSourceDocument source:
                    await AppendSourceAsync(source, token);
                    break;
                case string path:
                    await AppendMplsAsync(path, token);
                    break;
            }
        }, parameter => CanAppendMpls && parameter is ChapterSourceDocument or string);
        DropPathLoadCommand = new UiCommand(async (parameter, token) =>
        {
            if (parameter is ChapterSourceDocument source)
            {
                await LoadSourceAsync(source, token);
            }
            else
            {
                await LoadPathAsync(parameter?.ToString() ?? string.Empty, token);
            }
        });
        SaveCommand = new UiCommand(async (parameter, token) => await SaveAsync(parameter?.ToString(), token), _ => CurrentInfo is not null);
    }

    private void InitializeEditCommands()
    {
        UndoCommand = new UiCommand(async (_, token) => await NavigateHistoryAsync("undo", null, token), _ => CanUndo);
        RedoCommand = new UiCommand(async (_, token) => await NavigateHistoryAsync("redo", null, token), _ => CanRedo);
        NavigateHistoryCommand = new UiCommand(async (parameter, token) =>
        {
            if (parameter is Guid nodeId)
            {
                await NavigateHistoryAsync("navigate", nodeId, token);
            }
        }, parameter => parameter is Guid nodeId && HistoryEntries.Any(entry => entry.Id == nodeId));
        RefreshCommand = new UiCommand((_, _) =>
        {
            ApplyFrameInfo();
            return ValueTask.CompletedTask;
        }, _ => CanEditRows);
        ChangeFpsCommand = new UiCommand((_, token) => ChangeFpsToSelectedOption(token), _ => CanEditRows && selectedFrameRateOption.IsValid);
        SelectClipCommand = new UiCommand((parameter, _) =>
        {
            SelectClip(Convert.ToInt32(parameter));
            return ValueTask.CompletedTask;
        }, parameter => parameter is int index and >= 0 && index < ClipOptions.Count);
        CombineCommand = new UiCommand((_, _) =>
        {
            CombineSegments();
            return ValueTask.CompletedTask;
        }, _ => CanCombine);
        EditTimeCommand = new UiCommand(parameter => EditCell(parameter, EditKind.Time));
        EditNameCommand = new UiCommand(parameter => EditCell(parameter, EditKind.Name));
        EditFrameCommand = new UiCommand(parameter => EditCell(parameter, EditKind.Frame));
        DeleteCommand = new UiCommand(async (parameter, token) =>
        {
            if (CurrentInfo is not null && parameter is IReadOnlySet<int> indexes)
            {
                var preview = ClipEditingCoordinator.Delete(indexes, EditingOptions);
                var outcome = await ClipEditingCoordinator.ApplyCandidateAsync(preview, token);
                ApplyContentOutcome(outcome, $"Delete rows: indexes={string.Join(",", indexes.Order())}");
            }
        }, _ => CanEditRows);
        InsertCommand = new UiCommand(async (parameter, token) =>
        {
            if (CurrentInfo is not null)
            {
                var index = parameter is int value ? value : Rows.Count;
                var preview = ClipEditingCoordinator.InsertBefore(index);
                var outcome = await ClipEditingCoordinator.ApplyCandidateAsync(preview, token);
                ApplyContentOutcome(outcome, $"Insert row: index={index}");
            }
        }, _ => CanEditRows);
        PreviewContentOptionsCommand = new UiCommand((_, _) =>
        {
            RefreshContentOptionsPreview();
            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
        ApplyContentPreviewCommand = new UiCommand(async (_, token) =>
        {
            if (pendingContentPreview is null || !pendingContentPreview.IsValid)
            {
                return;
            }

            var preview = pendingContentPreview;
            var previewText = ExpressionPreviewText;
            isApplyingContentPreview = true;
            pendingContentPreview = null;
            OnPropertyChanged(nameof(IsContentPreviewPending));
            OnPropertyChanged(nameof(CanPreviewContentOptions));
            OnPropertyChanged(nameof(CanApplyContentPreview));
            OnPropertyChanged(nameof(CanRefreshContentPreview));
            OnPropertyChanged(nameof(IsChapterGridReadOnly));
            OnPropertyChanged(nameof(CanEditRows));
            TransactionOutcome outcome;
            try
            {
                outcome = await ApplyContentPreviewAsync(preview, token);
            }
            finally
            {
                isApplyingContentPreview = false;
                OnPropertyChanged(nameof(IsChapterGridReadOnly));
                OnPropertyChanged(nameof(CanEditRows));
            }
            if (pendingContentPreview is null
                && outcome.Kind is not (TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange))
            {
                pendingContentPreview = preview;
                ExpressionPreviewText = previewText;
            }
            else if (outcome.Kind is TransactionOutcomeKind.Committed or TransactionOutcomeKind.NoChange)
            {
                ExpressionPreviewText = string.Empty;
            }

            OnPropertyChanged(nameof(IsContentPreviewPending));
            OnPropertyChanged(nameof(CanPreviewContentOptions));
            OnPropertyChanged(nameof(CanApplyContentPreview));
            OnPropertyChanged(nameof(CanRefreshContentPreview));
            ApplyContentPreviewCommand.RaiseCanExecuteChanged();
            RefreshContentPreviewCommand.RaiseCanExecuteChanged();
            CancelContentPreviewCommand.RaiseCanExecuteChanged();
        }, _ => CanApplyContentPreview);
        RefreshContentPreviewCommand = new UiCommand((_, _) =>
        {
            if (pendingContentPreview is not { } stalePreview || !CanRefreshContentPreview)
            {
                return ValueTask.CompletedTask;
            }

            pendingContentPreview = stalePreview.Operation switch
            {
                "Apply expression" => PrepareExpressionOperation(Expression),
                "Change chapter frame rate" => PrepareFrameRateOperation(
                    ResolveSourceFrameRate(),
                    selectedFrameRateOption.Value),
                _ => PrepareContentOptionsOperation()
            };
            ExpressionPreviewText = BuildExpressionPreviewText(pendingContentPreview, Localizer);
            StatusText = Localizer.GetString(pendingContentPreview.IsValid
                ? "Expression.State.Ready"
                : "Expression.State.Invalid");
            OnPropertyChanged(nameof(IsContentPreviewPending));
            OnPropertyChanged(nameof(CanApplyContentPreview));
            OnPropertyChanged(nameof(CanRefreshContentPreview));
            ApplyContentPreviewCommand.RaiseCanExecuteChanged();
            RefreshContentPreviewCommand.RaiseCanExecuteChanged();
            CancelContentPreviewCommand.RaiseCanExecuteChanged();
            return ValueTask.CompletedTask;
        }, _ => CanRefreshContentPreview);
        CancelContentPreviewCommand = new UiCommand((_, _) =>
        {
            if (expressionPreviewTimer.IsEnabled)
            {
                expressionPreviewTimer.Stop();
                ExpressionPreviewText = string.Empty;
                OnPropertyChanged(nameof(IsContentPreviewPending));
                OnPropertyChanged(nameof(CanPreviewContentOptions));
                ApplyContentPreviewCommand.RaiseCanExecuteChanged();
                CancelContentPreviewCommand.RaiseCanExecuteChanged();
                return ValueTask.CompletedTask;
            }

            if (pendingContentPreview is { } preview)
            {
                CancelContentPreview(preview);
                pendingContentPreview = null;
                expressionPreviewTimer.Stop();
                ExpressionPreviewText = string.Empty;
                OnPropertyChanged(nameof(IsContentPreviewPending));
                OnPropertyChanged(nameof(CanPreviewContentOptions));
                OnPropertyChanged(nameof(CanApplyContentPreview));
                OnPropertyChanged(nameof(CanRefreshContentPreview));
                SetStatus("Status.Updated");
            }

            ApplyContentPreviewCommand.RaiseCanExecuteChanged();
            CancelContentPreviewCommand.RaiseCanExecuteChanged();
            return ValueTask.CompletedTask;
        }, _ => IsContentPreviewPending);
    }

    private async ValueTask NavigateHistoryAsync(string action, Guid? nodeId, CancellationToken cancellationToken)
    {
        InvalidatePendingContentPreview();

        var session = Workspace.ContentSession;
        if (session is null)
        {
            return;
        }

        var outcome = action switch
        {
            "undo" => await session.UndoAsync(cancellationToken),
            "redo" => await session.RedoAsync(cancellationToken),
            _ => await session.NavigateToAsync(nodeId!.Value, cancellationToken)
        };
        if (outcome.Kind == HistoryNavigationOutcomeKind.Committed)
        {
            Workspace.PublishContentDocument(outcome.Snapshot.Document);
            RefreshRows();
            StatusText = Localizer.GetString(action == "undo" ? "History.UndoApplied" : "History.RedoApplied");
        }
        else if (outcome.Kind is not HistoryNavigationOutcomeKind.NoChange)
        {
            StatusText = outcome.Errors.FirstOrDefault() ?? Localizer.GetString("History.NavigationFailed");
        }

        NotifyStateChanged();
    }

    private void InitializeWindowCommands()
    {
        PreviewCommand = WindowCommand("preview", () => CurrentInfo is not null);
        LogCommand = WindowCommand("log");
        SettingsCommand = WindowCommand("settings");
        LanguageCommand = WindowCommand("language");
        ExpressionCommand = WindowCommand("expression");
        TemplateNamesCommand = WindowCommand("template-names", () => CanOpenContentTools);
        ZonesCommand = WindowCommand("zones", () => CanOpenContentTools);
        ForwardShiftCommand = WindowCommand("forward-shift", () => CanOpenContentTools);
        HistoryCommand = WindowCommand("history", () => CanOpenHistory);
        OpenRelatedMediaCommand = new UiCommand(async (parameter, token) => await OpenRelatedMediaAsync(parameter, token), _ => RelatedMediaReferences.Count > 0);
    }
}
