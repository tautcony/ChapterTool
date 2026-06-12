## Context

The Avalonia rewrite already has UI-independent Core export and transform services, plus a compact main-window save workflow. The legacy parity review in `docs/legacy-new-implementation-diff.md` identifies six remaining gaps that are related enough to implement together: celltimes export, Chapter2Qpfile, XML export formatting/UID compatibility, rounding policy, ChangeFps, and full XML chapter language selection.

The key constraint is to restore behavior without reintroducing WinForms control coupling. Conversion and transform behavior belongs in Core contracts; Avalonia should only expose those contracts through commands, option lists, and tool windows.

## Goals / Non-Goals

**Goals:**

- Restore the named legacy export and transform workflows as tested Core behavior.
- Keep byte-level export compatibility where the old format had known downstream expectations.
- Make restored workflows discoverable in Avalonia without adding a large always-visible section to the main workflow.
- Preserve UTF-8 correctness for user-facing Chinese strings and exported text.

**Non-Goals:**

- Recreate WinForms absolute layout, dialogs, or control state.
- Restore unrelated legacy features such as self-update, file association, registry writes, or Win32-only UI helpers.
- Replace the existing Core exporter architecture or external-tool discovery system.

## Decisions

1. Add Core conversion services instead of ViewModel-only helpers.

   Celltimes and Chapter2Qpfile should be implemented as UI-independent services so tests can cover the conversion rules without Avalonia. Avalonia can provide a small tool entry point or reuse an existing text tool surface. The alternative, embedding conversion logic in a tool window ViewModel, would make parity harder to test and would repeat the legacy UI-coupled shape.

2. Treat XML compatibility as explicit exporter options with legacy-compatible defaults for Matroska chapter export.

   XML export should produce a declaration, legacy comments/doctype-compatible structure where applicable, formatted output, and non-trivial UIDs. The Core exporter should own this output contract. If existing callers need compact output later, that can be a separate option, but the primary user-facing XML format should match legacy expectations.

3. Centralize documented rounding policies in Core time formatting/conversion helpers.

   Boundary rounding must be deterministic and match the legacy call sites: timestamp text formatting follows the legacy `Math.Round` banker behavior, while frame display and several frame-number workflows keep their existing explicit frame rounding policy. Tests should cover half-millisecond and frame-boundary cases. This avoids exporter-specific rounding drift while avoiding a false one-size-fits-all rounding rule.

4. Implement ChangeFps as a frame-preserving transform.

   ChangeFps should compute each chapter's original frame number at the source FPS and then recalculate the chapter time at the target FPS. If a chapter has a duration/end, the duration/end should be recalculated from preserved frame span rather than from raw seconds.

5. Use an ISO language catalog for XML language selection.

   The UI should expose a searchable or otherwise scalable list of ISO language codes and display names while still supporting quick defaults such as `und`, `zh`, `ja`, and `en`. The saved/exported value remains the stable language code.

## Risks / Trade-offs

- XML UID randomness can make snapshot tests brittle -> assert structure and UID validity instead of fixed literal IDs, except where stable fallback is intentional.
- Full ISO language lists can clutter the main surface -> expose through a compact selector/tool and keep common values easy to reach.
- Chapter2Qpfile timecode support depends on precise legacy interpretation -> add focused tests with representative timecode input before broad UI work.
- Changing rounding behavior can affect many exports -> add regression tests around the formatter and affected export services before updating UI.
