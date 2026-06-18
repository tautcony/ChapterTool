using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Localization;
using ChapterTool.Avalonia.ViewModels;
using ChapterTool.Avalonia.Views.Tools;

namespace ChapterTool.Avalonia.Tests.Headless;

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
            Assert.NotNull(MainWindowHeadlessTestHost.RequiredDescendant<TextBox>(expressionWindow, _ => true, "expression text box"));
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
            Assert.True(MainWindowHeadlessTestHost.ContainsRenderedTextStatic(window, "Platform"));
            Assert.Contains(MainWindowHeadlessTestHost.Descendants<TextBox>(window), textBox => textBox.Text == viewModel.SaveDirectory);
            Assert.Contains(MainWindowHeadlessTestHost.Descendants<ComboBox>(window), comboBox => comboBox.ItemsSource == viewModel.Languages);
            Assert.Contains(MainWindowHeadlessTestHost.Descendants<Button>(window), button => button.Command == viewModel.SaveCommand);
        }
        finally
        {
            window.Close();
        }
    }
}
