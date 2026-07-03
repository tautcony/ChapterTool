## Context

The current Avalonia composition root constructs one `InMemoryApplicationLogService` and passes it to `MainWindowViewModel`. That service stores a list of `ApplicationLogEntry` records and formats them for the log window. This is useful for the UI, but it is also the primary logging implementation: there is no standard `ILogger<T>` pipeline, no built-in severity filtering, no provider/sink model, and no durable log output for post-failure troubleshooting.

There is also an active `localize-ui-logs-i18n` change that treats user-facing log entries as message keys with arguments and technical details. This logging change should preserve that model: localization decides how entries are displayed, while the logging middleware decides how structured diagnostic events are captured, filtered, and written.

## Goals / Non-Goals

**Goals:**

- Use standard .NET logging abstractions (`Microsoft.Extensions.Logging`) at application service boundaries.
- Use a widely adopted structured logging backend, preferably Serilog through the Microsoft logging integration, for runtime providers/sinks.
- Record logs with explicit severity, structured state, timestamps, categories, and optional exception/technical detail.
- Keep the existing log window workflow by introducing an in-memory UI sink/provider that exposes recent entries through `IApplicationLogService`.
- Add a durable rolling file sink under the app data/settings area so user reports can include logs after the app closes.
- Keep log volume useful: routine workflow summaries at `Information`, recoverable diagnostics at `Warning`, failures at `Error`, developer-only details at `Debug`, and very noisy internals at `Trace` only when explicitly enabled.

**Non-Goals:**

- Do not redesign import/export behavior or chapter data models.
- Do not make Core depend on Avalonia, Serilog concrete types, or UI localization resources.
- Do not replace the localized log-message work from `localize-ui-logs-i18n`; this change should compose with it.
- Do not add remote telemetry, network upload, or privacy-sensitive collection.

## Decisions

1. Use `Microsoft.Extensions.Logging` as the code-facing logging contract.

   Application and infrastructure services should depend on `ILogger<T>` or a small app-specific adapter backed by `ILogger`, not on Serilog concrete APIs. This keeps tests simple and avoids coupling Core or Infrastructure to one backend. Alternative considered: use Serilog directly everywhere. That gives richer APIs but spreads provider-specific types across the application and makes future backend changes harder.

2. Configure Serilog as the default desktop logging backend in the Avalonia composition root.

   Serilog is a common .NET structured logging library and gives file sinks, level filtering, enrichers, and structured output without building those features by hand. The composition root should create and own the logger factory, configure sinks, and dispose it during application shutdown where practical. Alternative considered: only use the built-in console/debug providers. That reduces dependencies but does not solve rolling file output or structured sink extensibility as cleanly.

3. Keep `IApplicationLogService` as the UI-facing log buffer, but make it a sink over the logging pipeline.

   Existing ViewModels and tool windows already know how to display and clear application log entries. Rather than removing that surface, implement an in-memory logging provider/sink that captures selected events into bounded `ApplicationLogEntry` records. `IApplicationLogService` becomes the read/clear API for the log window, while writes should flow through `ILogger` or logging helper methods. Alternative considered: let the log window read the rolling file. That is slower, harder to localize at display time, and makes clear behavior ambiguous.

4. Preserve structured message keys and technical details.

   Log events that are user-facing should carry the localization key, arguments, severity, category, and optional technical detail in structured state. The file sink can write the raw structured event, while the log window formats entries through the current localizer. This matches the direction of `localize-ui-logs-i18n` and avoids freezing display language at write time.

5. Use conservative default level routing.

   The UI buffer should keep recent `Information`, `Warning`, and `Error` events that are meaningful to users and support troubleshooting. The file sink should include `Debug` and above by default, with `Trace` reserved for explicit opt-in configuration. Exceptions and external process failures should include stack traces, command metadata, exit code, stdout/stderr summaries, and relevant paths as structured data where available.

## Risks / Trade-offs

- [Risk] Adding Serilog and Microsoft logging packages increases dependency surface. -> Mitigation: keep Serilog isolated in Avalonia composition and use `ILogger` abstractions elsewhere.
- [Risk] Log entries can expose local file paths or command output. -> Mitigation: log only local diagnostics, avoid remote upload, and keep file logs under the user's app data area with bounded retention.
- [Risk] Debug-level file logging may become noisy. -> Mitigation: keep UI filtering stricter, use rolling files with retention, and reserve `Trace` for explicit opt-in.
- [Risk] The active localization change may touch the same log service contracts. -> Mitigation: implement severity as additive metadata on structured entries and keep message-key formatting responsibilities separate from provider configuration.

## Migration Plan

1. Add logging package references and a composition-owned logger factory.
2. Extend `ApplicationLogEntry` with severity/category/exception metadata while preserving message key, arguments, timestamp, and technical detail.
3. Implement an in-memory logging provider/sink that captures bounded recent entries and backs `IApplicationLogService`.
4. Replace ViewModel direct `logService.Add(...)` writes with `ILogger<MainWindowViewModel>` or a logging helper backed by `ILogger`.
5. Add rolling file output and lifecycle disposal in the Avalonia app.
6. Update tests for severity mapping, UI log formatting, clear behavior, and composition resolution.
