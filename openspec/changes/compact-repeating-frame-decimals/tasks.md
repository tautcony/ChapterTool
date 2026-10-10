## 1. Settings contract and persistence

- [x] 1.1 Add the default-enabled ShowRepeatingFrameDecimals preference to application settings and the equivalent Web settings model. Preserve explicit false through normalization.
- [x] 1.2 Add settings tests for missing fields, false round trips, no rewrite on load, version-one compatibility, and preservation of unrelated aggregate sections.

## 2. Shared exact-number presentation

- [x] 2.1 Expose authoritative rational metadata for supported rate options and typed presentation inputs. Keep unknown or approximate rate provenance on the ordinary numeric path.
- [x] 2.2 Add the shared structured frame formatter with integer arithmetic, shortest-cycle detection, non-repeating prefixes, separate signs, a six-digit cycle limit, and a 512-step work limit.
- [x] 2.3 Implement rounding-first precedence and the existing decimal-place fallback. Keep display parts separate from frame text stored in domain and history models.
- [x] 2.4 Add formatter coverage for 1249.(006993), 1249.0(117882), 5233.9(948051), 9792, and 9792.0(047952). Include finite decimals, negative fractions, leading zeros, unknown provenance, and long cycles.
- [x] 2.5 Calculate authoritative frame values, rounding, fixed-place formatting, accuracy, and expression comparisons from timestamp ticks and rational rates. Keep approximate rates on the existing numeric path.
- [x] 2.6 Add exact arithmetic tests for tick/rate products, rounding boundaries, tolerance boundaries, and before/candidate values with independent rates.

## 3. Settings-only controls and localization

- [x] 3.1 Add the Avalonia General settings checkbox beside existing frame preferences. Live-apply it, preserve it during rounding changes, disable its control in integer mode, and restore it on discard.
- [x] 3.2 Add the equivalent Web settings draft control. Activate it only after successful Save. Preserve current reset, Cancel, error, and close behavior.
- [x] 3.3 Add Chinese, English, and Japanese labels and helper text to the shared locale resources. Generate Web JSON with uv run --project scripts scripts/axaml-to-json.py and verify with --check.
- [x] 3.4 Verify save, reload, reset, discard, integer-mode availability, and absence of recurring-decimal controls on the main workflow in settings ViewModel and Web tests.

## 4. Avalonia frame rendering

- [x] 4.1 Feed committed, original, and candidate presentation from their own typed snapshots and rate basis. Use one formatter policy and retain the original baseline.
- [x] 4.2 Render one continuous overline over repetend glyphs. Align decimal points within equal before/after areas and keep the arrow area stable. Preserve existing semantic font, colors, and narrow layout.
- [x] 4.3 Retain the single-value cell for unchanged frames. Refresh presentation without changing candidate identity, document content, history, row identity, selection, or scroll.
- [x] 4.4 Preserve plain numeric editing, unchanged editor round trips, ordinary numeric copy, full-value details, missing-versus-zero states, and accessible side descriptions.

## 5. Web frame rendering

- [x] 5.1 Connect chapter cells and frame-valued review surfaces to the shared formatter and saved preference. Preserve each side's rate basis.
- [x] 5.2 Render overlines and stable decimal alignment with the existing responsive table or narrow comparison layout.
- [x] 5.3 Preserve plain numeric editing and copy, accessible details, single-value unchanged cells, and candidate readiness when presentation refreshes.

## 6. Behavior and layout verification

- [x] 6.1 Add Avalonia ViewModel and Headless tests for t + 0.0002 at 24000/1001, shared before/after modes, unchanged cells, zero/missing states, and preferences changed during a current preview.
- [x] 6.2 Add Web workspace and browser coverage for the same fixture, settings Save and Cancel, numeric editing, and responsive comparisons.
- [x] 6.3 Verify that presentation changes leave candidate commit, Undo, Redo, and export content unchanged, including QPFile numeric output.
- [x] 6.4 Run affected Core, Infrastructure, Avalonia, Headless, and Web tests in the existing project-specific verification paths. Run .NET test projects sequentially and use --no-restore when assets are present.
- [x] 6.5 Capture and review default, wide, and narrow layouts in both themes and with enlarged monospace text. Store screenshots under artifacts/compact-repeating-frame-decimals/ and pair them with behavior assertions.
- [x] 6.6 Update relevant code-map ownership and test references if the formatter or runtime wiring introduces new primary entry points. Run openspec validate compact-repeating-frame-decimals --strict.

## 7. Explicit full-precision display mode

- [x] 7.1 Add and persist the `full-precision` frame display mode alongside rounding and fixed decimal places.
- [x] 7.2 Synchronize the Settings mode, main rounding control, and frame formatting policy in Avalonia and Web.
- [x] 7.3 Enable the repeating-cycle setting only in full-precision mode, retain its value across mode changes, and localize the new option.
- [x] 7.4 Add Core, settings, and browser/Headless coverage for all three modes, including full-precision cycle rendering and ordinary fallback.

## 8. Auto frame-rate consistency

- [x] 8.1 Keep the selected Auto option separate from the detected effective option in Avalonia and Web.
- [x] 8.2 Use the effective rational rate for frame display, editing, expression preview, and frame-shift operations while Auto remains selected. Detect Auto in all frame display modes.
- [x] 8.3 Add Core, Avalonia, and Web regression coverage for repeating decimals and exact frame shifts in Auto mode.

## 9. Preview comparison presentation regressions

- [x] 9.1 Center frame values across their cells and provide an unclipped arrow area in wide and narrow comparisons.
- [x] 9.2 Preserve the current expression candidate when toggling the main frame-rounding presentation option.

## 10. Live settings presentation refresh

- [x] 10.1 Refresh existing frame rows when Settings changes the display mode, including decimal places to full precision while integer rounding remains off.

## 11. Full-precision frame editor round trips

- [x] 11.1 Preserve the chapter ticks and full-precision frame text when an unchanged decimal frame value is committed with exact rational or decimal frame rates.

## 12. Narrow frame preview layout

- [x] 12.1 Keep the before and candidate frame values on two rows and align the arrow with the candidate value. Cover the layout with an Avalonia Headless assertion.

## 13. Settings-driven frame presentation startup

- [x] 13.1 Initialize the non-persistent main-window rounding control from the saved frame display mode. Keep main-window toggles session-only and ensure rounding suppresses cycle notation.
