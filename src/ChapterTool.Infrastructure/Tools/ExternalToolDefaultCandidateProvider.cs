namespace ChapterTool.Infrastructure.Tools;

public sealed class ExternalToolDefaultCandidateProvider : IExternalToolDefaultCandidateProvider
{
    public static ExternalToolDefaultCandidateProvider Instance { get; } = new();

    private ExternalToolDefaultCandidateProvider()
    {
    }

    public IEnumerable<string> FindCandidates(string toolId, string executableName) =>
        ExternalToolPathResolver.DefaultCandidates(toolId, executableName);
}
