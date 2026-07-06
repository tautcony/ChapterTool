using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ChapterTool.Avalonia.Composition;

namespace ChapterTool.Avalonia;

public sealed class App : Application
{
    private AppCompositionRoot? composition;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            composition = new AppCompositionRoot(Program.GuiStartupPath);
            desktop.Exit += (_, _) => composition.Dispose();
            desktop.MainWindow = composition.CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
