namespace ChapterTool.Infrastructure.Tools;

public interface IExternalToolDefaultCandidateProvider
{
    IEnumerable<string> FindCandidates(string toolId, string executableName);
}
