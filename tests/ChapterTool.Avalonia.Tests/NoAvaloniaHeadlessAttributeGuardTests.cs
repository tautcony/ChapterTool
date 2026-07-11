using System.Reflection;

namespace ChapterTool.Avalonia.Tests;

/// <summary>
/// Keeps Avalonia Headless UI tests out of this assembly so they run in a separate process
/// (<c>ChapterTool.Avalonia.Headless.Tests</c>) and cannot deadlock with parallel unit tests.
/// </summary>
public sealed class NoAvaloniaHeadlessAttributeGuardTests
{
    [Fact]
    public void Assembly_does_not_define_AvaloniaFact_or_AvaloniaTheory_tests()
    {
        var offenders = typeof(NoAvaloniaHeadlessAttributeGuardTests)
            .Assembly
            .GetTypes()
            .Where(static type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(HasAvaloniaTestAttribute)
                .Select(method => $"{type.FullName}.{method.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool HasAvaloniaTestAttribute(MethodInfo method) =>
        method.GetCustomAttributesData().Any(static attribute =>
        {
            var name = attribute.AttributeType.Name;
            return string.Equals(name, "AvaloniaFactAttribute", StringComparison.Ordinal)
                || string.Equals(name, "AvaloniaTheoryAttribute", StringComparison.Ordinal);
        });
}
