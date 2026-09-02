using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using ChapterTool.Avalonia.UI.PlatformPorts;
using ChapterTool.Avalonia.UI.ViewModels;
using ChapterTool.Avalonia.UI.Views.Controls;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Session;

namespace ChapterTool.Avalonia.UI.Views;

/// <summary>Provides the shared ChapterTool workflow surface.</summary>
public sealed partial class MainView : UserControl
{
    private static readonly IReadOnlyDictionary<Key, string> ControlGestures = new Dictionary<Key, string>
    {
        [Key.S] = "Ctrl+S", [Key.O] = "Ctrl+O", [Key.R] = "Ctrl+R", [Key.L] = "Ctrl+L"
    };

    private static readonly IReadOnlyDictionary<Key, string> FunctionGestures = new Dictionary<Key, string>
    {
        [Key.F5] = "F5", [Key.F11] = "F11", [Key.PageUp] = "PageUp", [Key.PageDown] = "PageDown"
    };

    private static readonly IReadOnlyDictionary<Key, string> AltNumberGestures = CreateNumberGestures("Alt+");
    private static readonly IReadOnlyDictionary<Key, string> ControlNumberGestures = CreateNumberGestures("Ctrl+");

    private readonly MainWindowViewModel viewModel;
    private readonly ShortcutRouter shortcutRouter;
    private readonly Func<Control, IFilePickerService> filePickerServiceFactory;
    private readonly UiOperationBoundary uiOperationBoundary;
    private readonly IEmbeddedToolPresenter embeddedToolPresenter;
    private IFilePickerService? filePickerService;
    private bool windowCommandRefreshPending;
    private bool commandStateSubscribed;
    private bool? advancedOptionsNarrow;

    public MainView()
    {
        throw new InvalidOperationException("MainWindow must be created by the application composition root.");
    }

    public MainView(
        MainWindowViewModel viewModel,
        Func<Control, IFilePickerService> filePickerServiceFactory)
        : this(viewModel, filePickerServiceFactory, new NoContentEmbeddedToolPresenter())
    {
    }

    public MainView(
        MainWindowViewModel viewModel,
        Func<Control, IFilePickerService> filePickerServiceFactory,
        IEmbeddedToolPresenter embeddedToolPresenter)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(filePickerServiceFactory);
        ArgumentNullException.ThrowIfNull(embeddedToolPresenter);
        this.viewModel = viewModel;
        this.filePickerServiceFactory = filePickerServiceFactory;
        this.embeddedToolPresenter = embeddedToolPresenter;
        shortcutRouter = new ShortcutRouter(viewModel);
        uiOperationBoundary = new UiOperationBoundary(viewModel.ReportUnexpectedUiException);

        // UI-only adapter commands: pickers and DataGrid selection. All other
        // commands bind to MainWindowViewModel so CanExecute has a single owner.
        BrowseAndLoadCommand = new UiCommand(async (_, _) => await BrowseAndLoadAsync());
        AppendMplsCommand = new UiCommand(async (_, _) => await AppendMplsAsync(), _ => viewModel.CanAppendMpls);
        LoadChapterNameTemplateCommand = new UiCommand(async (_, _) => await LoadChapterNameTemplateAsync());
        LoadLuaExpressionScriptCommand = new UiCommand(async (_, _) => await LoadLuaExpressionScriptAsync());
        InsertSelectedCommand = new UiCommand(async (_, _) => await InsertSelectedAsync(), _ => viewModel.InsertCommand.CanExecute(null));
        DeleteSelectedCommand = new UiCommand(async (_, _) => await DeleteSelectedAsync(), _ => viewModel.DeleteCommand.CanExecute(null));
        OpenZonesCommand = new UiCommand(async (_, _) => await OpenZonesAsync(), _ => viewModel.Rows.Count > 0);
        OpenForwardShiftCommand = new UiCommand(async (_, _) => await OpenForwardShiftAsync(), _ => viewModel.Rows.Count > 0);

        viewModel.SetUiErrorHandler(viewModel.ReportUnexpectedUiException);
        foreach (var command in UiAdapterCommands())
        {
            command.ErrorHandler = viewModel.ReportUnexpectedUiException;
        }

        InitializeComponent();
        DataContext = viewModel;
        UpdateSecondarySurface();
        ApplyAdvancedOptionsLayout();
        RaiseCommandStates();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        filePickerService ??= filePickerServiceFactory(this);
        if (commandStateSubscribed)
        {
            return;
        }

        embeddedToolPresenter.ContentChanged += OnSecondarySurfaceChanged;
        SubscribeViewModelCommandState();
        commandStateSubscribed = true;
        UpdateSecondarySurface();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyAdvancedOptionsLayout();

    private void OnExpressionEditorMultilineExpansionChanged(
        object? sender,
        ExpressionEditorExpansionChangedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var nextHeight = Math.Max(window.MinHeight, window.Height + args.HeightDelta);
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is not null)
        {
            var scaling = window.RenderScaling <= 0 ? 1 : window.RenderScaling;
            var workingHeight = screen.WorkingArea.Height / scaling;
            nextHeight = Math.Min(nextHeight, workingHeight);
        }

        window.Height = nextHeight;
    }

    internal static bool IsNarrowAdvancedOptions(double width) => width <= 860;

    public UiCommand BrowseAndLoadCommand { get; }

    public UiCommand AppendMplsCommand { get; }

    public UiCommand LoadChapterNameTemplateCommand { get; }

    public UiCommand LoadLuaExpressionScriptCommand { get; }

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
        var source = await FilePickerService.PickSourceDocumentAsync(CancellationToken.None);
        if (source is null)
        {
            return;
        }

        viewModel.SourcePath = source is LocalPathChapterSource local ? local.Path : source.DisplayName;
        await viewModel.LoadCommand.ExecuteAsync(source);
    }

    private async Task AppendMplsAsync()
    {
        var source = await FilePickerService.PickMplsDocumentAsync(CancellationToken.None);
        if (source is null)
        {
            return;
        }

        await viewModel.AppendMplsCommand.ExecuteAsync(source);
    }

    private async Task LoadChapterNameTemplateAsync()
    {
        var path = await FilePickerService.PickChapterNameTemplateAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.LoadChapterNameTemplateFromPathAsync(path, CancellationToken.None);
    }

    private async Task LoadLuaExpressionScriptAsync()
    {
        var path = await FilePickerService.PickLuaExpressionScriptAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.ToolSession.Expression.LoadScriptAsync(path, CancellationToken.None);
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

    public Task InitializeAsync(string? startupPath = null) => uiOperationBoundary.RunAsync(async () =>
    {
        await viewModel.LoadSettingsAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(startupPath))
        {
            viewModel.SourcePath = startupPath;
            await LoadAsync();
        }
    });

    private async void OnChapterGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs args) =>
        await uiOperationBoundary.RunAsync(async () => await CommitCellEditAsync(args).AsTask());

    private async void OnDrop(object? sender, DragEventArgs args) => await uiOperationBoundary.RunAsync(async () =>
    {
        var source = await FilePickerService.ConvertDropAsync(args.DataTransfer, CancellationToken.None);
        if (source is null)
        {
            return;
        }

        viewModel.SourcePath = source is LocalPathChapterSource local ? local.Path : source.DisplayName;
        await viewModel.DropPathLoadCommand.ExecuteAsync(source);
    });

    private IFilePickerService FilePickerService =>
        filePickerService ?? throw new InvalidOperationException("The shared main view must be attached before file actions can run.");

    private async void OnKeyDown(object? sender, KeyEventArgs args) => await uiOperationBoundary.RunAsync(async () =>
    {
        if (IsTextInputKeyScope(args.Source as Visual))
        {
            return;
        }

        if (await TryHandleEditKeyAsync(args))
        {
            return;
        }

        var gesture = Gesture(args);
        if (gesture is null)
        {
            return;
        }

        args.Handled = true;
        if (await TryHandleKnownGestureAsync(gesture) || TryHandleSaveFormatGesture(gesture))
        {
            return;
        }

        await shortcutRouter.RouteAsync(gesture);
    });

    private async Task<bool> TryHandleEditKeyAsync(KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Insert:
                args.Handled = true;
                await viewModel.InsertCommand.ExecuteAsync(SelectedRowIndex());
                return true;
            case Key.Delete:
                args.Handled = true;
                await viewModel.DeleteCommand.ExecuteAsync(SelectedIndexes());
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> TryHandleKnownGestureAsync(string gesture)
    {
        switch (gesture)
        {
            case "Ctrl+S":
                await viewModel.SaveCommand.ExecuteAsync();
                return true;
            case "Ctrl+O":
                await BrowseAndLoadAsync();
                return true;
            case "PageUp" or "PageDown":
            {
                var next = gesture == "PageUp" ? viewModel.SelectedClipIndex - 1 : viewModel.SelectedClipIndex + 1;
                if (viewModel.SelectClipCommand.CanExecute(next))
                {
                    await viewModel.SelectClipCommand.ExecuteAsync(next);
                }

                return true;
            }
            default:
                return false;
        }
    }

    private bool TryHandleSaveFormatGesture(string gesture)
    {
        if (!gesture.StartsWith("Alt+", StringComparison.Ordinal) || !int.TryParse(gesture["Alt+".Length..], out var saveIndex))
        {
            return false;
        }

        var mapped = saveIndex == 0 ? ChapterExportFormats.All.Count - 1 : saveIndex - 1;
        if (mapped >= 0 && mapped < ChapterExportFormats.All.Count)
        {
            viewModel.SaveFormatIndex = mapped;
        }

        return true;
    }

    private bool IsTextInputKeyScope(Visual? source)
    {
        if (IsTextInputVisual(source))
        {
            return true;
        }

        return TopLevel.GetTopLevel(this)?.FocusManager.GetFocusedElement() is Visual focused
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

        if (control && ControlGestures.TryGetValue(args.Key, out var controlGesture))
        {
            return controlGesture;
        }

        if (FunctionGestures.TryGetValue(args.Key, out var functionGesture))
        {
            return functionGesture;
        }

        if (alt && AltNumberGestures.TryGetValue(args.Key, out var altGesture))
        {
            return altGesture;
        }

        return control && ControlNumberGestures.TryGetValue(args.Key, out var numberGesture)
            ? numberGesture
            : null;
    }

    private static IReadOnlyDictionary<Key, string> CreateNumberGestures(string prefix)
    {
        var gestures = new Dictionary<Key, string>();
        for (var number = 0; number <= 9; number++)
        {
            gestures[(Key)((int)Key.D0 + number)] = $"{prefix}{number}";
            gestures[(Key)((int)Key.NumPad0 + number)] = $"{prefix}{number}";
        }

        return gestures;
    }

    private async ValueTask CommitCellEditAsync(DataGridCellEditEndedEventArgs args)
    {
        var edit = CreateCellEdit(args);
        if (edit is not null)
        {
            await edit();
        }
    }

    private Func<ValueTask>? CreateCellEdit(DataGridCellEditEndedEventArgs args)
    {
        var row = GetCommittedRow(args);
        if (row is null)
        {
            return null;
        }

        var index = viewModel.Rows.IndexOf(row);
        if (index < 0)
        {
            return null;
        }

        return CreateCellEditAction(index, row, ColumnTagId(args.Column));
    }

    private static ChapterRowViewModel? GetCommittedRow(DataGridCellEditEndedEventArgs args) =>
        args.EditAction == DataGridEditAction.Commit ? args.Row.DataContext as ChapterRowViewModel : null;

    private static string? ColumnTagId(DataGridColumn column) =>
        column.Tag as string ?? column.Tag?.ToString();

    private Func<ValueTask>? CreateCellEditAction(int index, ChapterRowViewModel row, string? columnId) => columnId switch
    {
        ChapterGridColumnIds.Time => () => viewModel.EditTimeCommand.ExecuteAsync(new ChapterCellEdit(index, row.TimeText)),
        ChapterGridColumnIds.Name => () => viewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(index, row.Name)),
        ChapterGridColumnIds.Frames => () => viewModel.EditFrameCommand.ExecuteAsync(new ChapterCellEdit(index, row.FramesInfo)),
        _ => null
    };

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
    [
        .. ChapterGrid.SelectedItems
            .OfType<ChapterRowViewModel>()
            .Select(row => viewModel.Rows.IndexOf(row))
            .Where(static index => index >= 0)
    ];

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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (commandStateSubscribed)
        {
            UnsubscribeViewModelCommandState();
            embeddedToolPresenter.ContentChanged -= OnSecondarySurfaceChanged;
            commandStateSubscribed = false;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnSecondarySurfaceChanged(object? sender, EventArgs args) => UpdateSecondarySurface();

    private void UpdateSecondarySurface()
    {
        SecondarySurface.Content = embeddedToolPresenter.Content;
        SecondarySurface.IsVisible = embeddedToolPresenter.Content is not null;
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
        yield return InsertSelectedCommand;
        yield return DeleteSelectedCommand;
        yield return OpenZonesCommand;
        yield return OpenForwardShiftCommand;
    }

    private void RaiseCommandStates()
    {
        AppendMplsCommand.RaiseCanExecuteChanged();
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
        var narrow = IsNarrowAdvancedOptions(layoutWidth);
        if (advancedOptionsNarrow == narrow)
        {
            return;
        }

        advancedOptionsNarrow = narrow;
        if (narrow)
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
        SetGridPosition(ExpressionOptionsGroup, 1, 1, 2);
    }

    private static void SetGridPosition(Control control, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, columnSpan);
    }
}
