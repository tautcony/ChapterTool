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
        RefreshCommand = new UiCommand((_, _) =>
        {
            ApplyFrameInfo();
            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
        ChangeFpsCommand = new UiCommand((_, _) =>
        {
            ChangeFpsToSelectedOption();
            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null && selectedFrameRateOption.IsValid);
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
        DeleteCommand = new UiCommand(parameter =>
        {
            if (CurrentInfo is not null && parameter is IReadOnlySet<int> indexes)
            {
                ApplyEdit(ClipEditingCoordinator.Delete(CurrentInfo, indexes, EditingOptions), EnglishLogText("Action.DeleteRows", ("indexes", string.Join(",", indexes.Order()))));
            }

            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
        InsertCommand = new UiCommand(parameter =>
        {
            if (CurrentInfo is not null)
            {
                var index = parameter is int value ? value : Rows.Count;
                ApplyEdit(ClipEditingCoordinator.InsertBefore(CurrentInfo, index), EnglishLogText("Action.InsertRow", ("index", index)));
            }

            return ValueTask.CompletedTask;
        }, _ => CurrentInfo is not null);
    }

    private void InitializeWindowCommands()
    {
        PreviewCommand = WindowCommand("preview", () => CurrentInfo is not null);
        LogCommand = WindowCommand("log");
        SettingsCommand = WindowCommand("settings");
        LanguageCommand = WindowCommand("language");
        ExpressionCommand = WindowCommand("expression");
        TemplateNamesCommand = WindowCommand("template-names");
        ZonesCommand = WindowCommand("zones");
        ForwardShiftCommand = WindowCommand("forward-shift");
        OpenRelatedMediaCommand = new UiCommand(async (parameter, token) => await OpenRelatedMediaAsync(parameter, token), _ => RelatedMediaReferences.Count > 0);
    }
}
