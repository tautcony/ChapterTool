using System.Xml.Linq;
using System.Text;

namespace ChapterTool.Avalonia.Tests;

public sealed class MainWindowXamlTests
{
    [Fact]
    public void MainWindowDeclaresInteractiveChapterToolSurface()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.Contains("<DataGrid", text, StringComparison.Ordinal);
        Assert.Contains("<DataGrid.ContextMenu>", text, StringComparison.Ordinal);
        Assert.Contains("ProgressBar", text, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"AdvancedOptions\"", text, StringComparison.Ordinal);
        Assert.Contains("AppendLoadMenuItem", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUsesResponsiveModernToolLayout()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.Contains("Background=\"#f3f4f6\"", text, StringComparison.Ordinal);
        Assert.Contains("Width=\"920\"", text, StringComparison.Ordinal);
        Assert.Contains("Height=\"720\"", text, StringComparison.Ordinal);
        Assert.Contains("Button.primaryAction", text, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", text, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", text, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,*,Auto\"", text, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"Auto,*,Auto\"", text, StringComparison.Ordinal);
        Assert.Contains("Content=\"载入\"", text, StringComparison.Ordinal);
        Assert.Contains("Content=\"保存\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("杞藉叆", text, StringComparison.Ordinal);
        Assert.DoesNotContain("淇濆瓨", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"时间点\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"章节名\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"帧数\"", text, StringComparison.Ordinal);
        Assert.Contains("Text=\"保存格式\"", text, StringComparison.Ordinal);
        Assert.Contains("Text=\"XML语言\"", text, StringComparison.Ordinal);
        Assert.Contains("WindowStartupLocation=\"CenterScreen\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowDoesNotUseAbsoluteCanvasLayout()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.DoesNotContain("<Canvas", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Canvas.Left", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Canvas.Top", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowProtectsGridAndNumericTextFromClipping()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.Contains("x:Name=\"OrderShiftBox\"", text, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"124\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"#\" Binding=\"{Binding Number}\" IsReadOnly=\"True\" Width=\"64\" MinWidth=\"56\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"时间点\" Binding=\"{Binding TimeText}\" Width=\"170\" MinWidth=\"132\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"章节名\" Binding=\"{Binding Name}\" Width=\"*\" MinWidth=\"180\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"帧数\" Binding=\"{Binding FramesInfo}\" Width=\"150\" MinWidth=\"96\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowKeepsStatusProgressVisibleAndUnclipped()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);
        var document = XDocument.Parse(text);
        var progressBar = document.Descendants()
            .Single(static element => element.Attributes().Any(static attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "ProgressBar"));

        Assert.Contains("ColumnDefinitions=\"*,Auto\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ProgressBar\"", text, StringComparison.Ordinal);
        Assert.Contains("Width=\"180\"", text, StringComparison.Ordinal);
        Assert.Contains("Height=\"10\"", text, StringComparison.Ordinal);
        Assert.Contains("Margin=\"12,0,16,0\"", text, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Center\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain(progressBar.Attributes(), static attribute =>
            attribute.Name.LocalName == "IsVisible" && attribute.Value == "False");
    }

    [Fact]
    public void MainWindowUsesStructuredAdvancedOptionsForResponsiveResizing()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.Contains("x:Name=\"AdvancedOptionsGrid\"", text, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,*\"", text, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"Auto,Auto,Auto\"", text, StringComparison.Ordinal);
        Assert.Contains("ColumnSpacing=\"18\"", text, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"AdvancedOptions\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ColumnDefinitions=\"Auto,180,Auto,Auto,Auto,Auto\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FormatOptionsGroup\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NamingOptionsGroup\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OrderShiftOptionsGroup\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"XmlLanguageOptionsGroup\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExpressionOptionsGroup\"", text, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"84,*\"", text, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"84,124\"", text, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"Auto,Auto,*\"", text, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", text, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", text, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"240\"", text, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"300\"", text, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"78\"", text, StringComparison.Ordinal);
        Assert.Contains("SizeChanged=\"OnSizeChanged\"", text, StringComparison.Ordinal);
        Assert.Contains("private void ApplyAdvancedOptionsLayout()", code, StringComparison.Ordinal);
        Assert.Contains("Bounds.Width >= 900", code, StringComparison.Ordinal);
        Assert.Contains("*,*,*", code, StringComparison.Ordinal);
        Assert.Contains("*,*", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowDoesNotExposeRegistryDependentFileAssociationAsPrimaryUi()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.DoesNotContain("FileAssociationButton", text, StringComparison.Ordinal);
        Assert.DoesNotContain("FileAssociationWindow", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LanguageButton", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LanguageWindow", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRefreshGuardsClipSelectionReentrancy()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.Contains("private bool isRefreshing", text, StringComparison.Ordinal);
        Assert.Contains("if (isRefreshing)", text, StringComparison.Ordinal);
        Assert.Contains("finally", text, StringComparison.Ordinal);
        Assert.Contains("isRefreshing = false", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowDoesNotUseHiddenCommandShimControls()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.DoesNotContain("x:Name=\"SaveToButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"AppendMplsButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"CombineButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"OpenMediaButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"ColorButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"ExpressionButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"TemplateButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"ZonesButton\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"ForwardShiftButton\"", text, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"SaveToMenuItem\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AppendLoadMenuItem\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ClipCombineMenuItem\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpenMediaMenuItem\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ZonesMenuItem\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ForwardShiftMenuItem\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowKeepsPrimaryLoadBrowsingAndShowsClipSelectorWhenAvailable()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.Contains("Command=\"{Binding #RootWindow.BrowseAndLoadCommand}\"", text, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding #RootWindow.ReloadCommand}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task LoadOrBrowseAsync()", code, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ClipBox\"", text, StringComparison.Ordinal);
        Assert.Contains("Grid.Column=\"1\"", text, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ClipCombineMenuItem\"", text, StringComparison.Ordinal);
        Assert.Contains("Header=\"Combine\"", text, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanCombine}\"", text, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding #RootWindow.CombineCommand}\"", text, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsClipSelectionVisible}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ClipBox.IsVisible = viewModel.IsClipSelectionVisible;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ClipBox.IsVisible = false;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowBindsRoutineStateInsteadOfRefreshingControls()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.Contains("Text=\"{Binding StatusText}\"", text, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding Progress}\"", text, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Rows}\"", text, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding ClipOptions}\"", text, StringComparison.Ordinal);
        Assert.Contains("SelectedIndex=\"{Binding SelectedClipIndex}\"", text, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding RoundFrames}\"", text, StringComparison.Ordinal);
        Assert.Contains("SelectedIndex=\"{Binding SelectedFrameRateIndex}\"", text, StringComparison.Ordinal);
        Assert.Contains("SelectedIndex=\"{Binding SaveFormatIndex}\"", text, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding UseTemplateNames}\"", text, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding AutoGenerateNames}\"", text, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding OrderShift}\"", text, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding ApplyExpression}\"", text, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Expression}\"", text, StringComparison.Ordinal);

        Assert.DoesNotContain("StatusBlock.Text = viewModel.StatusText", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ProgressBar.Value = viewModel.Progress", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ChapterGrid.ItemsSource", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ClipBox.ItemsSource", code, StringComparison.Ordinal);
        Assert.DoesNotContain("RoundFramesBox.IsChecked = viewModel.RoundFrames", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUsesTypedCompiledBindings()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);

        Assert.Contains("xmlns:vm=\"using:ChapterTool.Avalonia.ViewModels\"", text, StringComparison.Ordinal);
        Assert.Contains("x:DataType=\"vm:MainWindowViewModel\"", text, StringComparison.Ordinal);
        Assert.Contains("x:CompileBindings=\"True\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("x:CompileBindings=\"False\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUsesXamlCommandsInsteadOfConstructorClickRegistration()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"), Encoding.UTF8);
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.Contains("Command=\"{Binding #RootWindow.SaveCommand}\"", text, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PreviewCommand}\"", text, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding LogCommand}\"", text, StringComparison.Ordinal);
        Assert.Contains("Opened=\"OnOpened\"", text, StringComparison.Ordinal);
        Assert.Contains("SelectionChanged=\"OnClipSelectionChanged\"", text, StringComparison.Ordinal);
        Assert.Contains("CellEditEnded=\"OnChapterGridCellEditEnded\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain(".Click +=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectionChanged +=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("CellEditEnded +=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyDown +=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Opened +=", code, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowDoesNotFireAndForgetAsyncCommands()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml.cs"), Encoding.UTF8);

        Assert.DoesNotContain("_ = viewModel.", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Execute(null)", code, StringComparison.Ordinal);
        Assert.Contains("await viewModel.RefreshCommand.ExecuteAsync()", code, StringComparison.Ordinal);
        Assert.Contains("await viewModel.InsertCommand.ExecuteAsync(SelectedRowIndex())", code, StringComparison.Ordinal);
        Assert.Contains("await viewModel.DeleteCommand.ExecuteAsync(SelectedIndexes())", code, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowServiceUsesDedicatedToolViews()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Services", "AvaloniaWindowService.cs"), Encoding.UTF8);

        Assert.Contains("new TextToolView", code, StringComparison.Ordinal);
        Assert.Contains("new ColorSettingsView", code, StringComparison.Ordinal);
        Assert.Contains("new LanguageToolView", code, StringComparison.Ordinal);
        Assert.Contains("new ExpressionToolView", code, StringComparison.Ordinal);
        Assert.Contains("new TemplateNamesToolView", code, StringComparison.Ordinal);
        Assert.Contains("new ForwardShiftToolView", code, StringComparison.Ordinal);
        Assert.DoesNotContain(".Click +=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new TextBox {", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new CheckBox {", code, StringComparison.Ordinal);
        Assert.DoesNotContain("new NumericUpDown", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SourcePath")]
    [InlineData("LoadButton")]
    [InlineData("SaveButton")]
    [InlineData("ChapterGrid")]
    [InlineData("ClipSelector")]
    [InlineData("AdvancedOptions")]
    [InlineData("PreviewWindow")]
    [InlineData("LogWindow")]
    public void MainWindowExposesStableAutomationIds(string automationId)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "ChapterTool.Avalonia", "Views", "MainWindow.axaml"));
        var values = document.Descendants()
            .SelectMany(static element => element.Attributes())
            .Where(static attribute => attribute.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal))
            .Select(static attribute => attribute.Value)
            .ToArray();

        Assert.Contains(automationId, values);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Time_Shift.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
