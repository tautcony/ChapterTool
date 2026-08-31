using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using ChapterTool.Avalonia.UI.ViewModels.Tools;

namespace ChapterTool.Avalonia.UI.Views.Tools;

/// <summary>Provides the structured application log view.</summary>
public sealed partial class LogToolView : UserControl
{
    private const double NarrowLayoutThreshold = 820;
    private const double InspectorWidth = 420;
    private LogToolViewModel? subscribedViewModel;
    private bool previousDetailsOpen;
    private TextEditor? rawEditor;

    public LogToolView()
    {
        InitializeComponent();
        rawEditor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false,
            FontSize = 12,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
            Padding = new Thickness(6, 4),
            Document = new TextDocument()
        };
        rawEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Json");
        LogRawEditorHost.Content = rawEditor;
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        subscribedViewModel = DataContext as LogToolViewModel;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            previousDetailsOpen = subscribedViewModel.ShowDetails;
        }
        else
        {
            previousDetailsOpen = false;
        }

        ApplyResponsiveLayout();

        // SizeChanged is raised before descendants receive their final bounds.
        // Reapply once the layout pass has propagated the new width.
        Dispatcher.UIThread.Post(ApplyResponsiveLayout);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(LogToolViewModel.ShowDetails)
            or nameof(LogToolViewModel.HasSelectedEntry)
            or nameof(LogToolViewModel.SelectedEntry))
        {
            var detailsOpen = subscribedViewModel?.ShowDetails == true;
            var changed = detailsOpen != previousDetailsOpen;
            previousDetailsOpen = detailsOpen;
            ApplyResponsiveLayout();
            UpdateRawEditor();
            if (changed)
            {
                HideFlyouts();
                Dispatcher.UIThread.Post(detailsOpen ? FocusInspector : () => FocusEntry(subscribedViewModel?.SelectedEntry));
            }
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        if (args.PreviousSize.Width > 0 && Math.Abs(args.PreviousSize.Width - args.NewSize.Width) > 0.5)
        {
            HideFlyouts();
        }

        ApplyResponsiveLayout();
        Dispatcher.UIThread.Post(ApplyResponsiveLayout);
    }

    private void HideFlyouts()
    {
        LogFilterButton.Flyout?.Hide();
        LogMoreButton.Flyout?.Hide();
    }

    private void ApplyResponsiveLayout()
    {
        if (LogList is null || LogListHost is null || LogLayout is null || LogDetailsPanel is null || LogContentSurface is null)
        {
            return;
        }

        var width = LogContentSurface.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var narrow = width <= NarrowLayoutThreshold;
        var detailsOpen = DataContext is LogToolViewModel viewModel && viewModel.ShowDetails;

        LogLayout.ColumnDefinitions[0].Width = detailsOpen && narrow
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        LogLayout.ColumnDefinitions[1].Width = !detailsOpen
            ? new GridLength(0)
            : narrow
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(InspectorWidth, GridUnitType.Pixel);
        LogListHost.IsVisible = !detailsOpen || !narrow;
        LogDetailsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        LogDetailsPanel.Width = double.NaN;
        LogDetailsPanel.MaxWidth = narrow ? double.PositiveInfinity : InspectorWidth;
    }

    private void UpdateRawEditor()
    {
        if (rawEditor is null)
        {
            return;
        }

        var text = (DataContext as LogToolViewModel)?.SelectedEntry?.RawText ?? string.Empty;
        if (!string.Equals(rawEditor.Text, text, StringComparison.Ordinal))
        {
            rawEditor.Document = new TextDocument(text);
        }

    }

    private void FocusInspector()
    {
        if (LogDetailsCloseButton.IsVisible)
        {
            LogDetailsCloseButton.Focus();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (DataContext is not LogToolViewModel viewModel)
        {
            return;
        }

        if (args.Key == Key.Escape && viewModel.ShowDetails)
        {
            args.Handled = true;
            viewModel.CloseDetailsCommand.Execute(null);
            return;
        }

        if (args.Key is not (Key.Enter or Key.Space)
            || viewModel.SelectedEntry is not { } selected
            || !IsListInput(args.Source as Visual))
        {
            return;
        }

        args.Handled = true;
        viewModel.OpenDetailsCommand.Execute(selected);
    }

    private bool IsListInput(Visual? source)
    {
        return source is not null
            && (ReferenceEquals(source, LogList)
                || ((source.FindAncestorOfType<ListBox>() is { } list) && ReferenceEquals(list, LogList)));
    }

    private void FocusEntry(LogEntryViewModel? entry)
    {
        if (entry is not null && LogList.ContainerFromItem(entry) is Control container)
        {
            var detailsAction = container.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => button.Classes.Contains("logDetailsAction"));
            (detailsAction ?? container).Focus();
            return;
        }

        LogList.Focus();
    }
}
