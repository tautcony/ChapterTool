namespace ChapterTool.Core.Services;

public sealed record DialogRequest(string Title, string Message, DialogKind Kind);

public enum DialogKind
{
    Information,
    Warning,
    Error,
    Confirmation,
    TextInput
}

public sealed record DialogResult(bool Accepted, string? Text = null);
