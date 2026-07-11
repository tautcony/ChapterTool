using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Controls;
using ChapterTool.Avalonia.Views.Tools;
using ChapterTool.Core.Transform;

namespace ChapterTool.Avalonia.Tests.Headless;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class ToolViewsHeadlessTests
{
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
    public async Task Expression_editor_collapses_multiline_scripts_until_expanded()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            IsMultilineExpandable = true,
            Text = "local offset = 1\nreturn t + offset"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.False(editor.IsMultilineExpanded);
            Assert.Equal(25.6, editor.ActualEditorHeightForTesting);

            editor.SetCaretOffsetForTesting(6);
            editor.IsMultilineExpanded = true;
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(editor.IsMultilineExpanded);
            Assert.Equal(132, editor.ActualEditorHeightForTesting);
            Assert.Equal(6, editor.CaretOffsetForTesting);

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, string.Empty);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal("local ffset = 1\nreturn t + offset", editor.EditorText);
            Assert.Equal(6, editor.CaretOffsetForTesting);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_enters_multiline_without_losing_the_editing_session()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            IsMultilineExpandable = true,
            Text = "time"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.SetCaretOffsetForTesting(2);

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, string.Empty);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(editor.IsMultilineExpanded);
            Assert.Contains('\n', editor.EditorText);
            Assert.Equal("time", editor.EditorText.Replace("\r", string.Empty).Replace("\n", string.Empty));
            Assert.True(editor.CaretOffsetForTesting > 2);

            window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, string.Empty);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.Equal("time", editor.EditorText);
            Assert.False(editor.IsMultilineExpanded);
            Assert.Equal(2, editor.CaretOffsetForTesting);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_keeps_requested_multiline_height_when_compact_expansion_is_disabled()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            EditorHeight = 112,
            Text = "local offset = 1\nreturn t + offset"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.False(editor.IsMultilineExpanded);
            Assert.Equal(112, editor.ActualEditorHeightForTesting);
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
    public async Task Expression_editor_exposes_colored_preset_completion_namespace()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = "preset."
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.MoveCaretToEnd();
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            var completion = Assert.Single(editor.CurrentCompletions, item => item.Text == "preset.round-to-frame");
            Assert.Equal(ExpressionTokenKind.Snippet, completion.Kind);
            Assert.Equal("PRESET", completion.KindLabel);
            Assert.Contains("fps", completion.InsertText, StringComparison.Ordinal);
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
            Assert.Contains("Lua expression syntax error", editor.DiagnosticTooltipText, StringComparison.Ordinal);
            Assert.Contains("Add the missing operand", editor.DiagnosticTooltipText, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Expression_editor_delays_diagnostics_while_text_is_still_changing()
    {
        using var host = new MainWindowHeadlessTestHost(localizer: new AppLocalizationManager("en-US"));
        var editor = new ExpressionEditor
        {
            Localizer = host.Localizer,
            Text = "t"
        };
        var window = await MainWindowHeadlessTestHost.RenderToolAsync(editor, new object());
        try
        {
            editor.MoveCaretToEnd();
            editor.InsertTextForTesting(" +");
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.False(editor.HasDiagnostic);

            await Task.Delay(600);
            await MainWindowHeadlessTestHost.ExecuteLayoutAsync(window);

            Assert.True(editor.HasDiagnostic);
            Assert.Contains("Add the missing operand", editor.DiagnosticTooltipText, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("2+", "Lua expression syntax error", "Add the missing operand")]
    [InlineData("2^", "Lua expression syntax error", "Add the missing operand")]
    [InlineData("2?", "Unknown Lua token", "Check the Lua expression syntax")]
    [InlineData("floor(", "Lua expression syntax error", "Check the Lua expression syntax")]
    [InlineData("floor()", "Lua expression runtime error", "Check that referenced Lua variables and functions exist")]
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
}
