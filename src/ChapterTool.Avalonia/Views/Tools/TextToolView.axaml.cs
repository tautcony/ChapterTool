using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using ChapterTool.Avalonia.ViewModels;
using System.ComponentModel;

namespace ChapterTool.Avalonia.Views.Tools;

public sealed partial class TextToolView : UserControl
{
    private TextToolViewModel? subscribedViewModel;

    public TextToolView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs args)
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
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        subscribedViewModel = DataContext as TextToolViewModel;
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RebuildLines();
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
        LinesHost.Children.Clear();
        if (DataContext is not TextToolViewModel viewModel)
        {
            return;
        }

        foreach (var line in viewModel.Lines)
        {
            LinesHost.Children.Add(CreateLine(line));
        }
    }

    private static TextBlock CreateLine(TextToolLineViewModel line)
    {
        var text = new TextBlock
        {
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize = 13,
            LineHeight = 19,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new global::Avalonia.Thickness(10, 1, 12, 1)
        };

        text.Inlines ??= [];
        text.Inlines.Add(new Run($"{line.Number,4}  ")
        {
            Foreground = Brush("#8a94a6")
        });

        foreach (var span in line.Spans)
        {
            text.Inlines.Add(new Run(span.Text)
            {
                Foreground = ForegroundFor(span.Kind)
            });
        }

        return text;
    }

    private static IBrush ForegroundFor(TextToolSpanKind kind) =>
        kind switch
        {
            TextToolSpanKind.Name => Brush("#0550ae"),
            TextToolSpanKind.String => Brush("#116329"),
            TextToolSpanKind.Number => Brush("#953800"),
            _ => Brush("#24292f")
        };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
