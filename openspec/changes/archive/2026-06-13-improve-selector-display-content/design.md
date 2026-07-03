## Context

The main window binds the clip selector directly to `ChapterSourceOption.DisplayName`, and the XML language selector directly to a list of language code strings. Those values are also used for selection and export behavior, so replacing them in-place would risk changing functional behavior outside display.

## Goals / Non-Goals

**Goals:**
- Render clearer selector text in the main workflow.
- Preserve existing selected clip index and XML language code behavior.
- Keep the change scoped to UI-facing projection and tests.

**Non-Goals:**
- Do not change importer-generated `ChapterSourceOption` values.
- Do not change XML export language normalization or validation.
- Do not redesign the overall main-window layout.

## Decisions

- Add UI-facing display projections in `MainWindowViewModel` instead of changing Core models.
  - Rationale: selector text is presentation state; Core import/export contracts should remain stable.
  - Alternative considered: changing `ChapterSourceOption.DisplayName`; rejected because logs, tests, and importer parity can depend on those technical labels.

- Bind XML language display to catalog records while keeping `XmlLanguageOptions` as code strings.
  - Rationale: existing settings and export code use codes; adding display items avoids behavioral churn.
  - Alternative considered: changing `XmlLanguageOptions` to display names; rejected because that would ripple through settings and save behavior.

## Risks / Trade-offs

- Display projections can become stale if the source collection changes without notifications -> update projection notifications from existing clip collection and selection paths.
- Longer selector labels can crowd the top row -> constrain text with trimming and keep stable selector width behavior.
