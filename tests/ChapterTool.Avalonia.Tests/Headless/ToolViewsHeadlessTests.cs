using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Controls;
using ChapterTool.Avalonia.Views.Tools;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ToolViewsHeadlessTests
{
    [AvaloniaFact]
    public async Task Secondary_tool_views_render_and_bind_representative_state()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");

        var textToolViewModel = new TextToolViewModel(
            () => "CHAPTER01=00:00:00.000",
            new TextToolOptions { ClearAction = () => { } });
        var rendered = new List<Window>();
        try
        {
            rendered.Add(await MainWindowHeadlessTestHost.RenderToolAsync(
                new ColorSettingsView(),
                new ColorSettingsViewModel(host.ThemeSettingsStore)));
            rendered.Add(await MainWindowHeadlessTestHost.RenderToolAsync(
                new LanguageToolView(),
                new LanguageToolViewModel(host.ViewModel)));
            rendered.Add(await MainWindowHeadlessTestHost.RenderToolAsync(
                new ExpressionToolView(),
                new ExpressionToolViewModel(host.ViewModel)));
            rendered.Add(await MainWindowHeadlessTestHost.RenderToolAsync(
                new TemplateNamesToolView(),
                new TemplateNamesToolViewModel(host.ViewModel)));
            rendered.Add(await MainWindowHeadlessTestHost.RenderToolAsync(
                new ForwardShiftToolView(),
                new ForwardShiftToolViewModel(host.ViewModel)));
            rendered.Add(await MainWindowHeadlessTestHost.RenderToolAsync(
                new TextToolView(),
                textToolViewModel));

            Assert.Contains(rendered, window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Legacy color slots"));
            Assert.Contains(rendered, window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Language"));
            Assert.Contains(rendered, window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Expression"));
            Assert.Contains(rendered, window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Template Names"));
            Assert.Contains(rendered, window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Forward Shift"));
            Assert.Contains(rendered, window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Refresh"));
            Assert.Equal("CHAPTER01=00:00:00.000", Assert.Single(textToolViewModel.Lines).Spans.Single().Text);

            var expressionWindow = rendered.Single(window => MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Expression"));
            Assert.NotNull(MainWindowHeadlessTestHost.RequiredDescendant<ExpressionEditor>(expressionWindow, _ => true, "expression editor"));
            Assert.NotNull(MainWindowHeadlessTestHost.RequiredDescendant<CheckBox>(expressionWindow, _ => true, "expression apply checkbox"));
            Assert.NotNull(MainWindowHeadlessTestHost.RequiredDescendant<Button>(expressionWindow, button => button.Command is not null, "expression apply button"));
        }
        finally
        {
            foreach (var window in rendered)
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_shows_diagnostics_and_accepts_tab_completion()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = "t +"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            Assert.Contains("Add the missing operand before applying this token.", editor.DiagnosticTooltipText, StringComparison.Ordinal);

            editor.Text = "flo";
            editor.MoveCaretToEnd();
            editor.AcceptFirstCompletion();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal("floor()", editor.EditorText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_accepts_text_input_after_focus()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = string.Empty
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.InsertTextForTesting("t");
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal("t", editor.EditorText);
            Assert.Equal("t", editor.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_keeps_case_insensitive_prefix_completions_without_unknown_token_diagnostic()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = "S"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.MoveCaretToEnd();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Contains(editor.CurrentCompletions, item => item.Text == "sin");
            Assert.Contains(editor.CurrentCompletions, item => item.Text == "sqrt");
            Assert.False(editor.HasDiagnostic);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_opens_completion_popup_for_prefix_input_without_auto_showing_diagnostic_popup()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = "si"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.MoveCaretToEnd();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Contains(editor.CurrentCompletions, item => item.Text == "sin");
            Assert.False(editor.IsDiagnosticOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_reports_trailing_binary_operator()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = "2^"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.MoveCaretToEnd();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(editor.HasDiagnostic);
            Assert.Contains("requires more operands", editor.DiagnosticTooltipText, StringComparison.Ordinal);
            Assert.Contains("Add the missing operand", editor.DiagnosticTooltipText, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("2+", "requires more operands", "Add the missing operand")]
    [InlineData("2^", "requires more operands", "Add the missing operand")]
    [InlineData("2?", "matching ':'", "Add a matching ':'")]
    [InlineData("floor(", "Unbalanced parentheses", "every '(' has a matching ')'")]
    [InlineData("floor()", "Missing operand before ')'", "Add an operand before the closing parenthesis")]
    public async Task Expression_editor_tooltip_reports_incomplete_expression_errors(
        string expression,
        string expectedDiagnostic,
        string expectedSuggestion)
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = expression
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.MoveCaretToEnd();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(editor.HasDiagnostic);
            Assert.Contains(expectedDiagnostic, editor.DiagnosticTooltipText, StringComparison.Ordinal);
            Assert.Contains(expectedSuggestion, editor.DiagnosticTooltipText, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Text_tool_renders_selectable_text_for_copy_operations()
    {
        using var host = new MainWindowHeadlessTestHost();
        await host.LoadAsync("movie.txt");

        var textToolViewModel = new TextToolViewModel(
            () => "CHAPTER01=00:00:00.000\nCHAPTER02=00:00:05.000");

        var window = await MainWindowHeadlessTestHost.RenderToolAsync(new TextToolView(), textToolViewModel);
        try
        {
            var selectable = Assert.Single(MainWindowHeadlessTestHost.Descendants<SelectableTextBlock>(window));
            Assert.NotNull(selectable.Inlines);

            var runTexts = selectable.Inlines!
                .OfType<global::Avalonia.Controls.Documents.Run>()
                .Select(static run => run.Text)
                .Where(static text => !string.IsNullOrEmpty(text))
                .ToList();
            var selectableContent = string.Concat(runTexts);
            Assert.Equal("CHAPTER01=00:00:00.000CHAPTER02=00:00:05.000", selectableContent);

            var numberBlocks = MainWindowHeadlessTestHost.Descendants<TextBlock>(window)
                .Where(block => !string.IsNullOrWhiteSpace(block.Text))
                .Select(static block => block.Text)
                .ToList();
            Assert.Contains(numberBlocks, text => text!.Trim() == "1");
            Assert.Contains(numberBlocks, text => text!.Trim() == "2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Settings_tool_renders_grouped_configuration_controls()
    {
        using var host = new MainWindowHeadlessTestHost(
            localizer: new AppLocalizationManager("en-US"));
        var viewModel = new SettingsToolViewModel(
            host.ViewModel,
            host.AppSettingsStore,
            host.ThemeSettingsStore,
            host.Localizer,
            host.SettingsPickerService);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        var window = await MainWindowHeadlessTestHost.RenderToolAsync(new SettingsToolView(), viewModel);
        try
        {
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "General"));
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "External Tools"));

            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Output Defaults"));
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Appearance"));
            Assert.Contains(MainWindowHeadlessTestHost.Descendants<TextBox>(window), textBox => textBox.Text == viewModel.SaveDirectory);
            Assert.Contains(MainWindowHeadlessTestHost.Descendants<ComboBox>(window), comboBox => Equals(comboBox.ItemsSource, viewModel.Languages));
            Assert.Contains(MainWindowHeadlessTestHost.Descendants<Button>(window), button => button.Command == viewModel.SaveCommand);
        }
        finally
        {
            window.Close();
        }
    }
}
