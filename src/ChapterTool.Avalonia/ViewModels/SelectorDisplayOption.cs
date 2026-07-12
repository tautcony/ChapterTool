namespace ChapterTool.Avalonia.ViewModels;

public sealed class SelectorDisplayOption(string mainText, string remarkText, string displayText) : ObservableViewModel
{
    public string MainText
    {
        get;
        private set => SetProperty(ref field, value);
    } = mainText;

    public string RemarkText
    {
        get;
        private set => SetProperty(ref field, value);
    } = remarkText;

    public string DisplayText
    {
        get;
        private set => SetProperty(ref field, value);
    } = displayText;

    public void UpdateFrom(SelectorDisplayOption entry)
    {
        MainText = entry.MainText;
        RemarkText = entry.RemarkText;
        DisplayText = entry.DisplayText;
    }

    public override string ToString() => DisplayText;
}
