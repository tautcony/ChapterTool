## 1. Logging Dependencies and Composition

- [x] 1.1 Add `Microsoft.Extensions.Logging` abstractions and Serilog integration package references to the projects that own logging configuration and tests.
- [x] 1.2 Create a composition-owned logger factory in `AppCompositionRoot` with Serilog configured as the default backend.
- [x] 1.3 Configure local rolling file logging under the application data/settings area with bounded retention.
- [x] 1.4 Dispose or flush logging resources during Avalonia application shutdown where practical.

## 2. Application Log Model and UI Sink

- [x] 2.1 Extend `ApplicationLogEntry` to include severity level, category, event id/name where useful, exception text, and structured state while preserving message key, arguments, timestamp, and technical detail.
- [x] 2.2 Replace `InMemoryApplicationLogService` as the primary writer with an in-memory logging provider or sink that captures filtered `ILogger` events.
- [x] 2.3 Keep `IApplicationLogService` as the log-window read/clear API and enforce a bounded recent-entry limit.
- [x] 2.4 Preserve localized log formatting by passing message keys, arguments, and technical details through to the existing formatter path.

## 3. Logging Call-Site Migration

- [x] 3.1 Inject `ILogger<MainWindowViewModel>` or a small logging adapter into `MainWindowViewModel` through the composition root and tests.
- [x] 3.2 Replace direct `logService.Add(...)` write calls in the ViewModel with severity-specific structured logging helpers.
- [x] 3.3 Map routine workflow events to `Information`, recoverable diagnostics to `Warning`, failures/exceptions to `Error`, and developer-only implementation details to `Debug` or `Trace`.
- [x] 3.4 Add logging to external process/dependency failure paths where current diagnostics are insufficient for troubleshooting.

## 4. Verification and Tests

- [x] 4.1 Add infrastructure tests for the in-memory logging sink: severity capture, structured state capture, bounded retention, and clear behavior.
- [x] 4.2 Update Avalonia ViewModel tests to assert logs are produced through the logging pipeline and still format correctly for the log window.
- [x] 4.3 Add or update composition tests proving required logger services, UI log sink, and file logging configuration are registered.
- [x] 4.4 Run `dotnet test tests\ChapterTool.Infrastructure.Tests\ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 4.5 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 4.6 Run `dotnet build src\ChapterTool.Avalonia\ChapterTool.Avalonia.csproj --no-restore`.
