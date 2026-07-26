using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.ComponentModel;
using ChapterTool.Avalonia.ViewModels.Tools;

namespace ChapterTool.Avalonia.Views.Tools;

/// <summary>Provides the text tool view.</summary>
public sealed partial class TextToolView : UserControl
{
    private TextToolViewModel? subscribedViewModel;
    private UiOperationBoundary? uiOperationBoundary;

    public TextToolView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs args) =>
        await (uiOperationBoundary ??= new UiOperationBoundary(ReportUnexpectedException)).RunAsync(async () =>
    {
        if (DataContext is not TextToolViewModel viewModel)
        {
            return;
        }

        var window = TopLevel.GetTopLevel(this);
        if (window?.Clipboard is not null)
        {
            await window.Clipboard.SetTextAsync(viewModel.Text);
        }
    });

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            subscribedViewModel.DetachLiveRefresh();
        }

        subscribedViewModel = DataContext as TextToolViewModel;
        if (subscribedViewModel is not null)
        {
            uiOperationBoundary = new UiOperationBoundary(subscribedViewModel.ErrorHandler ?? ReportUnexpectedException);
            subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RebuildLines();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
        DataContextChanged -= OnDataContextChanged;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            subscribedViewModel.DetachLiveRefresh();
            subscribedViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TextToolViewModel.Lines))
        {
            RebuildLines();
        }
    }

    private void RebuildLines()
    {
        LineNumbersHost.Children.Clear();
        ContentText.Inlines?.Clear();
        if (DataContext is not TextToolViewModel viewModel)
        {
            return;
        }

        var lines = viewModel.Lines;
        if (lines.Count == 0)
        {
            return;
        }

        const double fontSize = 13;
        const double lineHeight = 19;

        ContentText.FontSize = fontSize;
        ContentText.LineHeight = lineHeight;
        ContentText.TextWrapping = TextWrapping.NoWrap;
        ContentText.Padding = new Thickness(8, 0, 12, 0);
        ContentText.Inlines ??= [];

        LineNumbersHost.Margin = new Thickness(0, 0, 0, 0);

        var inlines = ContentText.Inlines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            foreach (var span in line.Spans)
            {
                inlines.Add(new Run(span.Text)
                {
                    Foreground = ForegroundFor(span.Kind)
                });
            }

            var number = new TextBlock
            {
                FontSize = fontSize,
                LineHeight = lineHeight,
                Height = lineHeight,
                TextWrapping = TextWrapping.NoWrap,
                Padding = new Thickness(10, 0, 8, 0),
                Text = $"{line.Number,4}",
                Foreground = Brush("#8a94a6")
            };
            LineNumbersHost.Children.Add(number);
        }
    }

    private static IBrush ForegroundFor(TextToolSpanKind kind)
    {
        var key = kind switch
        {
            TextToolSpanKind.Name => "ChapterTool.Expression.VariableBrush",
            TextToolSpanKind.String => "ChapterTool.Expression.StringBrush",
            TextToolSpanKind.Number => "ChapterTool.Expression.NumberBrush",
            _ => "ChapterTool.ControlForegroundBrush"
        };

        return Application.Current?.Resources[key] as IBrush ?? Brush("#24292f");
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private ValueTask ReportUnexpectedException(Exception exception)
    {
        if (subscribedViewModel?.ErrorHandler is { } handler)
        {
            return handler(exception);
        }

        return ValueTask.CompletedTask;
    }
}
