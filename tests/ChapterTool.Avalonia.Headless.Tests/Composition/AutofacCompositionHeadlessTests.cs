using Autofac;
using Autofac.Core.Registration;
using Avalonia.Headless.XUnit;
using ChapterTool.Avalonia.Composition;
using ChapterTool.Avalonia.Headless.Tests.Headless;
using ChapterTool.Avalonia.UI.PlatformPorts;

namespace ChapterTool.Avalonia.Headless.Tests.Composition;

[Collection(AvaloniaHeadlessTestCollection.Name)]
public sealed class AutofacCompositionHeadlessTests
{
    [AvaloniaFact]
    public void Validation_fails_before_startup_when_a_required_registration_is_missing()
    {
        using var composition = new AppCompositionRoot(new AppCompositionOptions
        {
            RegisterProductionModules = false
        });

        Assert.Throws<ComponentNotRegisteredException>(composition.ValidateProductionComposition);
    }

    [AvaloniaFact]
    public void Test_registration_overrides_the_production_capabilities()
    {
        var replacement = new RuntimeCapabilities(
            RuntimeSourceMode.BufferedPortable,
            RuntimeOutputMode.Unavailable,
            RuntimeSecondarySurfaceMode.Unavailable,
            CanReadClipboard: false,
            CanWriteClipboard: false,
            CanConfigureExternalTools: false,
            CanRunExternalProcesses: false,
            CanOpenLocalPaths: false);
        using var composition = new AppCompositionRoot(new AppCompositionOptions
        {
            SettingsDirectory = CreateTempDirectory(),
            ConfigureOverrides = builder => builder.RegisterInstance(replacement).As<IRuntimeCapabilities>()
        });

        Assert.Same(replacement, composition.Capabilities);
        composition.ValidateProductionComposition();
    }

    [AvaloniaFact]
    public void Repeated_disposal_is_safe_and_disposes_an_override_once()
    {
        var probe = new DisposableCapabilities();
        var composition = new AppCompositionRoot(new AppCompositionOptions
        {
            SettingsDirectory = CreateTempDirectory(),
            ConfigureOverrides = builder => builder.RegisterInstance(probe).As<IRuntimeCapabilities>()
        });

        composition.Dispose();
        composition.Dispose();

        Assert.Equal(1, probe.DisposeCount);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ChapterTool.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class DisposableCapabilities : IRuntimeCapabilities, IDisposable
    {
        public RuntimeSourceMode SourceMode => RuntimeSourceMode.LocalPath;

        public RuntimeOutputMode OutputMode => RuntimeOutputMode.Directory;

        public RuntimeSecondarySurfaceMode SecondarySurfaceMode => RuntimeSecondarySurfaceMode.NativeWindow;

        public bool CanReadClipboard => true;

        public bool CanWriteClipboard => true;

        public bool CanConfigureExternalTools => true;

        public bool CanRunExternalProcesses => true;

        public bool CanOpenLocalPaths => true;

        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
