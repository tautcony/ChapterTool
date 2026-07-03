## Why

The Avalonia rewrite currently records application log entries through a small hand-written in-memory service, so log severity, filtering, sinks, and troubleshooting detail are not aligned with common .NET logging practices. Replacing this path with a widely used logging middleware will make diagnostics more consistent, easier to configure, and easier to expand without losing the existing log window workflow.

## What Changes

- Add a standard .NET logging backend for application diagnostics, using `Microsoft.Extensions.Logging` abstractions at service boundaries.
- Replace direct hand-written log storage as the primary logging mechanism with a logging service that records structured events, severity, timestamps, and optional technical details.
- Preserve the existing user-facing log window behavior by feeding it from an application log sink/provider that keeps recent entries in memory.
- Define severity usage so routine workflow events, recoverable issues, user-visible errors, and developer diagnostics are recorded at appropriate levels.
- Keep localized/user-facing log rendering compatible with the existing structured message-key direction from `localize-ui-logs-i18n`.
- Add tests that verify severity mapping, structured log capture, clear behavior, and ViewModel logging through injectable logging services.

## Capabilities

### New Capabilities

### Modified Capabilities

- `supporting-ui-platform-services`: Application logging must use a standard .NET logging middleware path with structured entries, severity levels, and a log-window-compatible in-memory sink.

## Impact

- Affected projects: `src/ChapterTool.Core`, `src/ChapterTool.Infrastructure`, `src/ChapterTool.Avalonia`.
- Affected tests: `tests/ChapterTool.Infrastructure.Tests`, `tests/ChapterTool.Avalonia.Tests`, and focused composition/logging tests.
- Likely dependency additions: `Microsoft.Extensions.Logging` and related logging package references appropriate for the app composition root and tests.
- Existing `IApplicationLogService` consumers may need method changes or adapters so log calls carry severity and structured state.
- No chapter import/export file format changes are expected.
