using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Controls;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.Views;

/// <summary>Provides the main ChapterTool application window.</summary>
public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly ShortcutRouter shortcutRouter;
    private readonly IFilePickerService filePickerService;
    private readonly string? startupPath;
    private readonly UiOperationBoundary uiOperationBoundary;
    private bool windowCommandRefreshPending;

    public MainWindow()
    {
        throw new InvalidOperationException("MainWindow must be created by the application composition root.");
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        Func<Window, IFilePickerService> filePickerServiceFactory,
        string? startupPath = null)
    {
        this.viewModel = viewModel;
        this.startupPath = startupPath;
        filePickerService = filePickerServiceFactory(this);
        shortcutRouter = new ShortcutRouter(viewModel);
        uiOperationBoundary = new UiOperationBoundary(viewModel.ReportUnexpectedUiException);

        // UI-only adapter commands: pickers and DataGrid selection. All other
        // commands bind to MainWindowViewModel so CanExecute has a single owner.
        BrowseAndLoadCommand = new UiCommand(async (_, _) => await BrowseAndLoadAsync());
        AppendMplsCommand = new UiCommand(async (_, _) => await AppendMplsAsync(), _ => viewModel.CanAppendMpls);
        LoadChapterNameTemplateCommand = new UiCommand(async (_, _) => await LoadChapterNameTemplateAsync());
        LoadLuaExpressionScriptCommand = new UiCommand(async (_, _) => await LoadLuaExpressionScriptAsync());
        SaveToCommand = new UiCommand(async (_, _) => await SaveToAsync(), _ => viewModel.SaveCommand.CanExecute());
        InsertSelectedCommand = new UiCommand(async (_, _) => await InsertSelectedAsync(), _ => viewModel.InsertCommand.CanExecute());
        DeleteSelectedCommand = new UiCommand(async (_, _) => await DeleteSelectedAsync(), _ => viewModel.DeleteCommand.CanExecute());
        OpenZonesCommand = new UiCommand(async (_, _) => await OpenZonesAsync(), _ => viewModel.Rows.Count > 0);
        OpenForwardShiftCommand = new UiCommand(async (_, _) => await OpenForwardShiftAsync(), _ => viewModel.Rows.Count > 0);

        viewModel.SetUiErrorHandler(viewModel.ReportUnexpectedUiException);
        foreach (var command in UiAdapterCommands())
        {
            command.ErrorHandler = viewModel.ReportUnexpectedUiException;
        }

        InitializeComponent();
        DataContext = viewModel;
        UpdateTitle();
        viewModel.Localizer.CultureChanged += (_, _) => UpdateTitle();
        SubscribeViewModelCommandState();
        ApplyAdvancedOptionsLayout();
        RaiseCommandStates();
    }

    public UiCommand BrowseAndLoadCommand { get; }

    public UiCommand AppendMplsCommand { get; }

    public UiCommand LoadChapterNameTemplateCommand { get; }

    public UiCommand LoadLuaExpressionScriptCommand { get; }

    public UiCommand SaveToCommand { get; }

    public UiCommand InsertSelectedCommand { get; }

    public UiCommand DeleteSelectedCommand { get; }

    public UiCommand OpenZonesCommand { get; }

    public UiCommand OpenForwardShiftCommand { get; }

    private async Task LoadAsync()
    {
        var path = string.IsNullOrWhiteSpace(viewModel.SourcePath)
            ? viewModel.CurrentPath
            : viewModel.SourcePath;
        await viewModel.LoadCommand.ExecuteAsync(path);
    }

    private async Task BrowseAndLoadAsync()
    {
        var path = await filePickerService.PickSourceAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        viewModel.SourcePath = path;
        await viewModel.LoadCommand.ExecuteAsync(path);
    }

    private async Task SaveToAsync()
    {
        var directory = await filePickerService.PickSaveDirectoryAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        await viewModel.SaveCommand.ExecuteAsync(directory);
    }

    private async Task AppendMplsAsync()
    {
        var path = await filePickerService.PickMplsAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.AppendMplsCommand.ExecuteAsync(path);
    }

    private async Task LoadChapterNameTemplateAsync()
    {
        var path = await filePickerService.PickChapterNameTemplateAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.LoadChapterNameTemplateFromPathAsync(path, CancellationToken.None);
    }

    private async Task LoadLuaExpressionScriptAsync()
    {
        var path = await filePickerService.PickLuaExpressionScriptAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.PortAdapters.Expression.LoadScriptAsync(path, CancellationToken.None);
    }

    private async Task OpenZonesAsync()
    {
        viewModel.UpdateSelectedRows(SelectedIndexes());
        await viewModel.ZonesCommand.ExecuteAsync();
    }

    private async Task OpenForwardShiftAsync()
    {
        viewModel.UpdateSelectedRows(SelectedIndexes());
        await viewModel.ForwardShiftCommand.ExecuteAsync();
    }

    private void UpdateTitle()
    {
        var baseTitle = viewModel.Localizer.GetString("App.Title");
        var version = typeof(MainWindow).Assembly.GetName().Version;
        Title = $"{baseTitle} v{version?.ToString(3) ?? "0.0.0"}";
    }

    private async void OnOpened(object? sender, EventArgs args) => await uiOperationBoundary.RunAsync(async () =>
    {
        await viewModel.LoadSettingsAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(startupPath))
        {
            viewModel.SourcePath = startupPath;
            await LoadAsync();
        }
    });

    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        ApplyAdvancedOptionsLayout();
    }

    private void OnExpressionEditorMultilineExpansionChanged(
        object? sender,
        ExpressionEditorExpansionChangedEventArgs args)
    {
        Height = Math.Max(MinHeight, Height + args.HeightDelta);
    }

    private async void OnChapterGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs args) =>
        await uiOperationBoundary.RunAsync(async () => await CommitCellEditAsync(args).AsTask());

    private async void OnDrop(object? sender, DragEventArgs args) => await uiOperationBoundary.RunAsync(async () =>
    {
        var files = args.DataTransfer.TryGetFiles()?.ToArray();
        var path = files?.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        viewModel.SourcePath = path;
        await viewModel.DropPathLoadCommand.ExecuteAsync(path);
    });

    private async void OnKeyDown(object? sender, KeyEventArgs args) => await uiOperationBoundary.RunAsync(async () =>
    {
        if (IsTextInputKeyScope(args.Source as Visual))
        {
            return;
        }

        var gesture = Gesture(args);
        switch (args.Key)
        {
            case Key.Insert:
                args.Handled = true;
                await viewModel.InsertCommand.ExecuteAsync(SelectedRowIndex());
                return;
            case Key.Delete:
                args.Handled = true;
                await viewModel.DeleteCommand.ExecuteAsync(SelectedIndexes());
                return;
        }

        if (gesture is null)
        {
            return;
        }

        args.Handled = true;
        switch (gesture)
        {
            case "Ctrl+S":
                await viewModel.SaveCommand.ExecuteAsync();
                return;
            case "Ctrl+O":
                await BrowseAndLoadAsync();
                return;
            case "PageUp" or "PageDown":
            {
                var next = gesture == "PageUp" ? viewModel.SelectedClipIndex - 1 : viewModel.SelectedClipIndex + 1;
                if (viewModel.SelectClipCommand.CanExecute(next))
                {
                    await viewModel.SelectClipCommand.ExecuteAsync(next);
                }

                return;
            }
        }

        if (gesture.StartsWith("Alt+", StringComparison.Ordinal) && int.TryParse(gesture["Alt+".Length..], out var saveIndex))
        {
            var mapped = saveIndex == 0 ? ChapterExportFormats.All.Count - 1 : saveIndex - 1;
            if (mapped >= 0 && mapped < ChapterExportFormats.All.Count)
            {
                viewModel.SaveFormatIndex = mapped;
            }

            return;
        }

        await shortcutRouter.RouteAsync(gesture);
    });

    private bool IsTextInputKeyScope(Visual? source)
    {
        if (IsTextInputVisual(source))
        {
            return true;
        }

        return FocusManager.GetFocusedElement() is Visual focused
            && IsTextInputVisual(focused);
    }

    private static bool IsTextInputVisual(Visual? source)
    {
        return source is TextBox or NumericUpDown or TextEditor or ExpressionEditor
            || source?.FindAncestorOfType<TextBox>() is not null
            || source?.FindAncestorOfType<NumericUpDown>() is not null
            || source?.FindAncestorOfType<TextEditor>() is not null
            || source?.FindAncestorOfType<ExpressionEditor>() is not null;
    }

    private static string? Gesture(KeyEventArgs args)
    {
        var control = args.KeyModifiers.HasFlag(KeyModifiers.Control);
        var alt = args.KeyModifiers.HasFlag(KeyModifiers.Alt);

        switch (control)
        {
            case true when args.Key == Key.S:
                return "Ctrl+S";
            case true when args.Key == Key.O:
                return "Ctrl+O";
            case true when args.Key == Key.R:
                return "Ctrl+R";
            case true when args.Key == Key.L:
                return "Ctrl+L";
        }

        switch (args.Key)
        {
            case Key.F5:
                return "F5";
            case Key.F11:
                return "F11";
            case Key.PageUp:
                return "PageUp";
            case Key.PageDown:
                return "PageDown";
        }

        if (alt)
        {
            return args.Key switch
            {
                Key.D0 or Key.NumPad0 => "Alt+0",
                Key.D1 or Key.NumPad1 => "Alt+1",
                Key.D2 or Key.NumPad2 => "Alt+2",
                Key.D3 or Key.NumPad3 => "Alt+3",
                Key.D4 or Key.NumPad4 => "Alt+4",
                Key.D5 or Key.NumPad5 => "Alt+5",
                Key.D6 or Key.NumPad6 => "Alt+6",
                Key.D7 or Key.NumPad7 => "Alt+7",
                Key.D8 or Key.NumPad8 => "Alt+8",
                Key.D9 or Key.NumPad9 => "Alt+9",
                _ => null
            };
        }

        if (!control)
        {
            return null;
        }

        return args.Key switch
        {
            Key.D0 or Key.NumPad0 => "Ctrl+0",
            Key.D1 or Key.NumPad1 => "Ctrl+1",
            Key.D2 or Key.NumPad2 => "Ctrl+2",
            Key.D3 or Key.NumPad3 => "Ctrl+3",
            Key.D4 or Key.NumPad4 => "Ctrl+4",
            Key.D5 or Key.NumPad5 => "Ctrl+5",
            Key.D6 or Key.NumPad6 => "Ctrl+6",
            Key.D7 or Key.NumPad7 => "Ctrl+7",
            Key.D8 or Key.NumPad8 => "Ctrl+8",
            Key.D9 or Key.NumPad9 => "Ctrl+9",
            _ => null
        };
    }

    private async ValueTask CommitCellEditAsync(DataGridCellEditEndedEventArgs args)
    {
        if (args.EditAction != DataGridEditAction.Commit || args.Row.DataContext is not ChapterRowViewModel row)
        {
            return;
        }

        var index = viewModel.Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        // Stable column identity via Tag — not localized header text.
        var columnId = args.Column.Tag as string
            ?? args.Column.Tag?.ToString();
        switch (columnId)
        {
            case ChapterGridColumnIds.Time:
                await viewModel.EditTimeCommand.ExecuteAsync(new ChapterCellEdit(index, row.TimeText));
                break;
            case ChapterGridColumnIds.Name:
                await viewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(index, row.Name));
                break;
            case ChapterGridColumnIds.Frames:
                await viewModel.EditFrameCommand.ExecuteAsync(new ChapterCellEdit(index, row.FramesInfo));
                break;
        }
    }

    private async ValueTask InsertSelectedAsync()
    {
        await viewModel.InsertCommand.ExecuteAsync(SelectedRowIndex());
    }

    private async ValueTask DeleteSelectedAsync()
    {
        await viewModel.DeleteCommand.ExecuteAsync(SelectedIndexes());
    }

    private int SelectedRowIndex() =>
        ChapterGrid.SelectedItem is ChapterRowViewModel row ? viewModel.Rows.IndexOf(row) : viewModel.Rows.Count;

    private HashSet<int> SelectedIndexes() =>
        ChapterGrid.SelectedItems
            .OfType<ChapterRowViewModel>()
            .Select(row => viewModel.Rows.IndexOf(row))
            .Where(static index => index >= 0)
            .ToHashSet();

    private void OnOrderShiftValueChanged(object? sender, NumericUpDownValueChangedEventArgs args)
    {
        // Keep NumericUpDown non-null; binding remains authoritative for OrderShift.
        OrderShiftBox.Value ??= 0;
    }

    private void SubscribeViewModelCommandState()
    {
        foreach (var command in ViewModelCommandsForAdapterRefresh())
        {
            command.CanExecuteChanged += ScheduleWindowCommandRefresh;
        }

        viewModel.Rows.CollectionChanged += ScheduleWindowCommandRefresh;
    }

    private void UnsubscribeViewModelCommandState()
    {
        foreach (var command in ViewModelCommandsForAdapterRefresh())
        {
            command.CanExecuteChanged -= ScheduleWindowCommandRefresh;
        }

        viewModel.Rows.CollectionChanged -= ScheduleWindowCommandRefresh;
    }

    protected override void OnClosed(EventArgs args)
    {
        UnsubscribeViewModelCommandState();
        base.OnClosed(args);
    }

    private IEnumerable<UiCommand> ViewModelCommandsForAdapterRefresh()
    {
        yield return viewModel.AppendMplsCommand;
        yield return viewModel.SaveCommand;
        yield return viewModel.InsertCommand;
        yield return viewModel.DeleteCommand;
        yield return viewModel.ZonesCommand;
        yield return viewModel.ForwardShiftCommand;
    }

    private IEnumerable<UiCommand> UiAdapterCommands()
    {
        yield return BrowseAndLoadCommand;
        yield return AppendMplsCommand;
        yield return LoadChapterNameTemplateCommand;
        yield return LoadLuaExpressionScriptCommand;
        yield return SaveToCommand;
        yield return InsertSelectedCommand;
        yield return DeleteSelectedCommand;
        yield return OpenZonesCommand;
        yield return OpenForwardShiftCommand;
    }

    private void RaiseCommandStates()
    {
        AppendMplsCommand.RaiseCanExecuteChanged();
        SaveToCommand.RaiseCanExecuteChanged();
        InsertSelectedCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
        OpenZonesCommand.RaiseCanExecuteChanged();
        OpenForwardShiftCommand.RaiseCanExecuteChanged();
    }

    private void ScheduleWindowCommandRefresh(object? sender, EventArgs e)
    {
        if (windowCommandRefreshPending)
        {
            return;
        }

        windowCommandRefreshPending = true;
        Dispatcher.UIThread.Post(ExecuteWindowCommandRefresh);
    }

    private void ExecuteWindowCommandRefresh()
    {
        windowCommandRefreshPending = false;
        RaiseCommandStates();
    }

    private void ApplyAdvancedOptionsLayout()
    {
        var layoutWidth = Bounds.Width > 0 ? Bounds.Width : Width;
        if (layoutWidth <= 760)
        {
            AdvancedOptionsGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
            AdvancedOptionsGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");

            SetGridPosition(FormatOptionsGroup, 0, 0);
            SetGridPosition(ChapterNameOptionsGroup, 0, 1);
            SetGridPosition(XmlLanguageOptionsGroup, 1, 0);
            SetGridPosition(OrderShiftOptionsGroup, 1, 1);
            SetGridPosition(ExpressionOptionsGroup, 2, 0, 2);
            return;
        }

        AdvancedOptionsGrid.ColumnDefinitions = new ColumnDefinitions("*,2*,*");
        AdvancedOptionsGrid.RowDefinitions = new RowDefinitions("Auto,Auto");

        SetGridPosition(FormatOptionsGroup, 0, 0);
        SetGridPosition(ChapterNameOptionsGroup, 0, 1);
        SetGridPosition(OrderShiftOptionsGroup, 0, 2);
        SetGridPosition(XmlLanguageOptionsGroup, 1, 0);
        SetGridPosition(ExpressionOptionsGroup, 1, 1);
    }

    private static void SetGridPosition(Control control, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, columnSpan);
    }
}
