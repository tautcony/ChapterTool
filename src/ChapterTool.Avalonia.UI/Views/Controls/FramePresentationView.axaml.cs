using Avalonia;
using Avalonia.Controls;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.UI.Views.Controls;

public partial class FramePresentationView : UserControl
{
    public static readonly StyledProperty<FramePresentationParts?> PresentationProperty =
        AvaloniaProperty.Register<FramePresentationView, FramePresentationParts?>(nameof(Presentation));

    public FramePresentationView()
    {
        InitializeComponent();
    }

    public FramePresentationParts? Presentation
    {
        get => GetValue(PresentationProperty);
        set => SetValue(PresentationProperty, value);
    }
}
