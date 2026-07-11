using System.Reflection;
using Avalonia.Headless.XUnit;

namespace ChapterTool.Avalonia.Headless.Tests.Headless;

public sealed class HeadlessTestCollectionGuardTests
{
    [Fact]
    public void Avalonia_tests_are_isolated_to_headless_collection()
    {
        var offenders = typeof(HeadlessAvaloniaTestApplication)
            .Assembly
            .GetTypes()
            .Where(static type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(HasAvaloniaTestAttribute))
            .Where(static type => !HasHeadlessCollection(type))
            .Select(static type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool HasAvaloniaTestAttribute(MethodInfo method) =>
        method.GetCustomAttributesData().Any(static attribute =>
            attribute.AttributeType == typeof(AvaloniaFactAttribute)
            || attribute.AttributeType == typeof(AvaloniaTheoryAttribute));

    private static bool HasHeadlessCollection(Type type) =>
        type.GetCustomAttributesData().Any(static attribute =>
            attribute.AttributeType == typeof(CollectionAttribute)
            && attribute.ConstructorArguments.Any(static argument =>
                string.Equals(argument.Value as string, AvaloniaHeadlessTestCollection.Name, StringComparison.Ordinal)));
}
