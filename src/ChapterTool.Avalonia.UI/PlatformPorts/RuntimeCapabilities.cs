namespace ChapterTool.Avalonia.UI.PlatformPorts;

public enum RuntimeSourceMode
{
    LocalPath,
    BufferedPortable
}

public enum RuntimeOutputMode
{
    Directory,
    BrowserDownload,
    Unavailable
}

public enum RuntimeSecondarySurfaceMode
{
    NativeWindow,
    InView,
    Unavailable
}

/// <summary>Describes host effects that the shared UI may expose.</summary>
public interface IRuntimeCapabilities
{
    RuntimeSourceMode SourceMode { get; }

    RuntimeOutputMode OutputMode { get; }

    RuntimeSecondarySurfaceMode SecondarySurfaceMode { get; }

    bool CanReadClipboard { get; }

    bool CanWriteClipboard { get; }

    bool CanConfigureExternalTools { get; }

    bool CanRunExternalProcesses { get; }

    bool CanOpenLocalPaths { get; }
}

public sealed record RuntimeCapabilities(
    RuntimeSourceMode SourceMode,
    RuntimeOutputMode OutputMode,
    RuntimeSecondarySurfaceMode SecondarySurfaceMode,
    bool CanReadClipboard,
    bool CanWriteClipboard,
    bool CanConfigureExternalTools,
    bool CanRunExternalProcesses,
    bool CanOpenLocalPaths) : IRuntimeCapabilities;
