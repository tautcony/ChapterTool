using ChapterTool.CommandLine;

namespace ChapterTool.Avalonia.Tests.Architecture;

public sealed class HostDependencyBoundaryTests
{
    [Fact]
    public void Avalonia_host_does_not_reference_command_line_host()
    {
        Assert.DoesNotContain(
            typeof(App).Assembly.GetReferencedAssemblies(),
            assembly => string.Equals(assembly.Name, "ChapterTool", StringComparison.Ordinal));
    }

    [Fact]
    public void Command_line_host_does_not_reference_avalonia()
    {
        Assert.DoesNotContain(
            typeof(ChapterToolCliHost).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name is not null
                && assembly.Name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
