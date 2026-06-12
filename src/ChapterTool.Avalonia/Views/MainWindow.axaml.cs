using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ChapterTool.Avalonia.Services;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Core.Exporting;

namespace ChapterTool.Avalonia.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly ShortcutRouter shortcutRouter;
    private readonly IFilePickerService filePickerService;
    private readonly string? startupPath;
    private bool isRefreshing;

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
        BrowseAndLoadCommand = new UiCommand(async (_, _) => await BrowseAndLoadAsync());
        ReloadCommand = new UiCommand(async (_, _) => await LoadAsync(), _ => viewModel.ReloadCommand.CanExecute());
        AppendMplsCommand = new UiCommand(async (_, _) => await AppendMplsAsync(), _ => viewModel.CanAppendMpls);
        LoadChapterNameTemplateCommand = new UiCommand(async (_, _) => await LoadChapterNameTemplateAsync());
        SaveCommand = new UiCommand(async (_, _) => await SaveAsync(null), _ => viewModel.SaveCommand.CanExecute());
        SaveToCommand = new UiCommand(async (_, _) => await SaveToAsync(), _ => viewModel.SaveCommand.CanExecute());
        PreviewCommand = new UiCommand(async (_, _) =>
        {
            ReadAdvancedOptions();
            await viewModel.PreviewCommand.ExecuteAsync();
        }, _ => viewModel.PreviewCommand.CanExecute());
        RefreshRowsCommand = new UiCommand(async (_, _) =>
        {
            ReadAdvancedOptions();
            ReadFrameOptions();
            await viewModel.RefreshCommand.ExecuteAsync();
            Refresh();
        }, _ => viewModel.RefreshCommand.CanExecute());
        InsertSelectedCommand = new UiCommand(async (_, _) =>
        {
            await InsertSelectedAsync();
        }, _ => viewModel.InsertCommand.CanExecute());
        DeleteSelectedCommand = new UiCommand(async (_, _) =>
        {
            await DeleteSelectedAsync();
        }, _ => viewModel.DeleteCommand.CanExecute());
        OpenRelatedMediaCommand = new UiCommand(async (_, _) => await OpenRelatedMediaAsync(), _ => viewModel.OpenRelatedMediaCommand.CanExecute());
        OpenZonesCommand = new UiCommand(async (_, _) => await OpenZonesAsync(), _ => viewModel.Rows.Count > 0);
        OpenForwardShiftCommand = new UiCommand(async (_, _) => await OpenForwardShiftAsync(), _ => viewModel.Rows.Count > 0);
        CombineCommand = new UiCommand(async (_, _) =>
        {
            await viewModel.CombineCommand.ExecuteAsync();
            Refresh();
        }, _ => viewModel.CombineCommand.CanExecute());

        InitializeComponent();
        DataContext = viewModel;
        ApplyAdvancedOptionsLayout();
        Refresh();
    }

    public UiCommand BrowseAndLoadCommand { get; }

    public UiCommand ReloadCommand { get; }

    public UiCommand AppendMplsCommand { get; }

    public UiCommand LoadChapterNameTemplateCommand { get; }

    public UiCommand SaveCommand { get; }

    public UiCommand SaveToCommand { get; }

    public UiCommand PreviewCommand { get; }

    public UiCommand RefreshRowsCommand { get; }

    public UiCommand InsertSelectedCommand { get; }

    public UiCommand DeleteSelectedCommand { get; }

    public UiCommand OpenRelatedMediaCommand { get; }

    public UiCommand OpenZonesCommand { get; }

    public UiCommand OpenForwardShiftCommand { get; }

    public UiCommand CombineCommand { get; }

    private async Task LoadAsync()
    {
        isRefreshing = true;
        try
        {
            await viewModel.LoadCommand.ExecuteAsync(PathBox.Text ?? string.Empty);
        }
        finally
        {
            isRefreshing = false;
        }
        Refresh();
    }

    private async Task BrowseAndLoadAsync()
    {
        var path = await filePickerService.PickSourceAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        PathBox.Text = path;
        isRefreshing = true;
        try
        {
            await viewModel.LoadCommand.ExecuteAsync(path);
        }
        finally
        {
            isRefreshing = false;
        }
        Refresh();
    }

    private async Task SaveAsync(string? directory)
    {
        ReadAdvancedOptions();
        viewModel.SaveFormat = (ChapterExportFormat)Math.Max(0, FormatBox.SelectedIndex);
        directory ??= string.IsNullOrWhiteSpace(viewModel.CurrentPath) ? null : Path.GetDirectoryName(viewModel.CurrentPath);
        await viewModel.SaveDirectoryCommand.ExecuteAsync(directory);
        Refresh();
    }

    private async Task SaveToAsync()
    {
        var directory = await filePickerService.PickSaveDirectoryAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        viewModel.SaveDirectory = directory;
        await SaveAsync(directory);
    }

    private async Task AppendMplsAsync()
    {
        var path = await filePickerService.PickMplsAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        isRefreshing = true;
        try
        {
            await viewModel.AppendMplsCommand.ExecuteAsync(path);
        }
        finally
        {
            isRefreshing = false;
        }
        Refresh();
    }

    private async Task LoadChapterNameTemplateAsync()
    {
        var path = await filePickerService.PickChapterNameTemplateAsync(CancellationToken.None);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var text = await ChapterNameTemplateReader.ReadAsync(path, CancellationToken.None);
        viewModel.ChapterNameTemplateText = text;
        viewModel.ChapterNameTemplateStatus = Path.GetFileName(path);
        viewModel.ChapterNameModeIndex = 2;
        Refresh();
    }

    private async Task OpenRelatedMediaAsync()
    {
        await viewModel.OpenRelatedMediaCommand.ExecuteAsync();
        Refresh();
    }

    private async Task OpenZonesAsync()
    {
        viewModel.UpdateSelectedRows(SelectedIndexes());
        await viewModel.ZonesCommand.ExecuteAsync();
        Refresh();
    }

    private async Task OpenForwardShiftAsync()
    {
        viewModel.UpdateSelectedRows(SelectedIndexes());
        await viewModel.ForwardShiftCommand.ExecuteAsync();
        Refresh();
    }

    private async void OnOpened(object? sender, EventArgs args)
    {
        await viewModel.LoadSettingsAsync(CancellationToken.None);
        Refresh();
        if (!string.IsNullOrWhiteSpace(startupPath))
        {
            PathBox.Text = startupPath;
            await LoadAsync();
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        ApplyAdvancedOptionsLayout();
    }

    private async void OnFrameOptionsChanged(object? sender, RoutedEventArgs args)
    {
        await ApplyFrameOptionsAndRefreshAsync();
    }

    private async void OnClipSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        if (ClipBox.SelectedIndex >= 0)
        {
            await viewModel.SelectClipCommand.ExecuteAsync(ClipBox.SelectedIndex);
            Refresh();
        }
    }

    private async void OnChapterGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs args)
    {
        await CommitCellEditAsync(args);
    }

    private async void OnDrop(object? sender, DragEventArgs args)
    {
        try
        {
            var files = args.DataTransfer.TryGetFiles()?.ToArray();
            var path = files?.FirstOrDefault()?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            PathBox.Text = path;
            isRefreshing = true;
            try
            {
                await viewModel.DropPathLoadCommand.ExecuteAsync(path);
            }
            finally
            {
                isRefreshing = false;
            }
            Refresh();
        }
        catch (Exception exception)
        {
            StatusBlock.Text = exception.Message;
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs args)
    {
        var gesture = Gesture(args);
        if (args.Key == Key.Insert)
        {
            args.Handled = true;
            await viewModel.InsertCommand.ExecuteAsync(SelectedRowIndex());
            Refresh();
            return;
        }

        if (args.Key == Key.Delete)
        {
            args.Handled = true;
            await viewModel.DeleteCommand.ExecuteAsync(SelectedIndexes());
            Refresh();
            return;
        }

        if (gesture is null)
        {
            return;
        }

        args.Handled = true;
        if (gesture is "Ctrl+S" or "Alt+S")
        {
            await SaveAsync(gesture == "Alt+S" ? viewModel.SaveDirectory : null);
            return;
        }

        if (gesture == "Ctrl+O")
        {
            await BrowseAndLoadAsync();
            return;
        }

        if (gesture is "PageUp" or "PageDown")
        {
            var next = gesture == "PageUp" ? viewModel.SelectedClipIndex - 1 : viewModel.SelectedClipIndex + 1;
            if (viewModel.SelectClipCommand.CanExecute(next))
            {
                await viewModel.SelectClipCommand.ExecuteAsync(next);
                Refresh();
            }

            return;
        }

        if (gesture.StartsWith("Alt+", StringComparison.Ordinal) && int.TryParse(gesture["Alt+".Length..], out var saveIndex))
        {
            var mapped = saveIndex == 0 ? 9 : saveIndex - 1;
            if (mapped >= 0 && mapped < FormatBox.ItemCount)
            {
                FormatBox.SelectedIndex = mapped;
                viewModel.SaveFormat = (ChapterExportFormat)mapped;
                Refresh();
            }

            return;
        }

        await shortcutRouter.RouteAsync(gesture);
        Refresh();
    }

    private string? Gesture(KeyEventArgs args)
    {
        var control = args.KeyModifiers.HasFlag(KeyModifiers.Control);
        var alt = args.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (control && args.Key == Key.S)
        {
            return "Ctrl+S";
        }

        if (control && args.Key == Key.O)
        {
            return "Ctrl+O";
        }

        if (alt && args.Key == Key.S)
        {
            return "Alt+S";
        }

        if (control && args.Key == Key.R)
        {
            return "Ctrl+R";
        }

        if (control && args.Key == Key.L)
        {
            return "Ctrl+L";
        }

        if (args.Key == Key.F5)
        {
            return "F5";
        }

        if (args.Key == Key.F11)
        {
            return "F11";
        }

        if (args.Key == Key.PageUp)
        {
            return "PageUp";
        }

        if (args.Key == Key.PageDown)
        {
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

        var columnIndex = ChapterGrid.Columns.IndexOf(args.Column);
        var header = args.Column.Header?.ToString();
        if (columnIndex == 1 || string.Equals(header, "Time", StringComparison.Ordinal) || header == "时间点")
        {
            await viewModel.EditTimeCommand.ExecuteAsync(new ChapterCellEdit(index, row.TimeText));
        }
        else if (columnIndex == 2 || string.Equals(header, "Name", StringComparison.Ordinal) || header == "章节名")
        {
            await viewModel.EditNameCommand.ExecuteAsync(new ChapterCellEdit(index, row.Name));
        }
        else if (columnIndex == 3 || string.Equals(header, "Frames", StringComparison.Ordinal) || header == "帧数")
        {
            await viewModel.EditFrameCommand.ExecuteAsync(new ChapterCellEdit(index, row.FramesInfo));
        }

        Refresh();
    }

    private async ValueTask InsertSelectedAsync()
    {
        await viewModel.InsertCommand.ExecuteAsync(SelectedRowIndex());
        Refresh();
    }

    private async ValueTask DeleteSelectedAsync()
    {
        await viewModel.DeleteCommand.ExecuteAsync(SelectedIndexes());
        Refresh();
    }

    private int SelectedRowIndex() =>
        ChapterGrid.SelectedItem is ChapterRowViewModel row ? viewModel.Rows.IndexOf(row) : viewModel.Rows.Count;

    private IReadOnlySet<int> SelectedIndexes() =>
        ChapterGrid.SelectedItems
            .OfType<ChapterRowViewModel>()
            .Select(row => viewModel.Rows.IndexOf(row))
            .Where(static index => index >= 0)
            .ToHashSet();

    private void ReadAdvancedOptions()
    {
        viewModel.ChapterNameModeIndex = Math.Max(0, ChapterNameModeBox.SelectedIndex);
        viewModel.ApplyExpression = ApplyExpressionBox.IsChecked == true;
        viewModel.Expression = ExpressionBox.Text ?? "t";
        viewModel.OrderShift = NormalizedOrderShiftValue();
    }

    private void OnOrderShiftValueChanged(object? sender, NumericUpDownValueChangedEventArgs args)
    {
        if (OrderShiftBox.Value is null)
        {
            OrderShiftBox.Value = 0;
        }
    }

    private int NormalizedOrderShiftValue()
    {
        if (OrderShiftBox.Value is { } value)
        {
            return (int)value;
        }

        OrderShiftBox.Value = 0;
        return 0;
    }

    private void ReadFrameOptions()
    {
        viewModel.SetFrameOptions(FrameRateBox.SelectedIndex, RoundFramesBox.IsChecked == true);
    }

    private async ValueTask ApplyFrameOptionsAndRefreshAsync()
    {
        if (isRefreshing)
        {
            return;
        }

        ReadFrameOptions();
        await viewModel.RefreshCommand.ExecuteAsync();
        Refresh();
    }

    private void Refresh()
    {
        isRefreshing = true;
        try
        {
            if (ClipBox.SelectedIndex != viewModel.SelectedClipIndex)
            {
                ClipBox.SelectedIndex = viewModel.SelectedClipIndex;
            }

            RaiseCommandStates();
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void RaiseCommandStates()
    {
        ReloadCommand.RaiseCanExecuteChanged();
        AppendMplsCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        SaveToCommand.RaiseCanExecuteChanged();
        PreviewCommand.RaiseCanExecuteChanged();
        RefreshRowsCommand.RaiseCanExecuteChanged();
        InsertSelectedCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
        OpenRelatedMediaCommand.RaiseCanExecuteChanged();
        OpenZonesCommand.RaiseCanExecuteChanged();
        OpenForwardShiftCommand.RaiseCanExecuteChanged();
        CombineCommand.RaiseCanExecuteChanged();
        viewModel.SaveCommand.RaiseCanExecuteChanged();
        viewModel.CombineCommand.RaiseCanExecuteChanged();
        viewModel.OpenRelatedMediaCommand.RaiseCanExecuteChanged();
        viewModel.PreviewCommand.RaiseCanExecuteChanged();
        viewModel.SettingsCommand.RaiseCanExecuteChanged();
        viewModel.ColorSettingsCommand.RaiseCanExecuteChanged();
        viewModel.ExpressionCommand.RaiseCanExecuteChanged();
        viewModel.TemplateNamesCommand.RaiseCanExecuteChanged();
    }

    private void ApplyAdvancedOptionsLayout()
    {
        AdvancedOptionsGrid.ColumnDefinitions = new ColumnDefinitions("*,2*,*");
        AdvancedOptionsGrid.RowDefinitions = new RowDefinitions("Auto,Auto");

        SetGridPosition(FormatOptionsGroup, 0, 0);
        SetGridPosition(ChapterNameOptionsGroup, 0, 1);
        SetGridPosition(OrderShiftOptionsGroup, 0, 2);
        SetGridPosition(XmlLanguageOptionsGroup, 1, 0);
        SetGridPosition(ExpressionOptionsGroup, 1, 1);
    }

    private static void SetGridPosition(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, 1);
    }

    private static string SelectedComboText(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem switch
        {
            ComboBoxItem item when item.Content is not null => item.Content.ToString() ?? fallback,
            string text when !string.IsNullOrWhiteSpace(text) => text,
            _ => fallback
        };
    }

}
