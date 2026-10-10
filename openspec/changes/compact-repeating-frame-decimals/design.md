## Context

The selected design concerns decimal frame presentation. An arrow compares a committed value with a candidate value. It does not represent rounding.

Current formatting has several owners:

- `FrameRateService` produces plain numeric frame text.
- `ChapterEditingOptions` and application settings define integer rounding, fixed decimal places, and full precision.
- `ChapterRowViewModel` retains the visible original frame baseline during preview.
- `ExpressionPreviewProjection` projects typed comparison values.
- Avalonia settings apply runtime-safe preferences before Save.
- Web settings activate saved drafts after persistence succeeds.

`FrameRateOption` currently stores a decimal rate and a stable code. Its decimal value alone does not prove an exact rational rate. These boundaries must remain explicit.

## Goals / Non-Goals

**Goals:**

- Match the selected overline display in committed and before/after frame cells.
- Put the preference in Settings → General, beside existing frame display preferences.
- Use one presentation policy for both sides.
- Preserve numeric editing, full-value inspection, semantic colors, and document behavior.
- Support the same presentation semantics in Avalonia and Web.

**Non-Goals:**

- Add a display selector, recurring-decimal toggle, or preview checkbox to the main workflow.
- Change rounding defaults, fixed decimal-place defaults, expression semantics, accuracy tolerance, history, or export.
- Add recurring-decimal notation to editable values, serialized chapter content, CLI output, or exported files.
- Redesign the existing settings lifecycle or expression review workflow.

## Decisions

### 1. Use one Settings-only boolean

Add `ShowRepeatingFrameDecimals`, serialized as `application.showRepeatingFrameDecimals`. Default it to `true`. Use the localized Chinese label “显示小数循环节”. Describe it as “未取整时，用上划线简化循环小数；关闭后按原小数格式显示。”

Keep the control visible but disabled while integer rounding is active. Retain its value when rounding changes. Place it beside the existing frame display and decimal-place preferences. Do not add a new settings tab.

Enable the checkbox only in the “do not truncate” mode. The fixed-place mode already defines an explicit truncation policy, and integer mode has no decimal cycle. Retain the checkbox value when switching modes. Apply the host's existing settings lifecycle. Avalonia live-applies the preference, saves it explicitly, and restores its saved value on discard. Web treats it as a draft and activates it after a successful Save. Neither host creates a chapter transaction for this preference.

Alternative: a selector above the chapter grid. Rejected because the user explicitly requested the preference in Settings.

### 2. Give compact recurring decimals a defined precedence

Use three frame display modes on every supported frame display surface: integer rounding, a configured number of decimal places, and no truncation. Use this order inside the selected mode:

1. Integer rounding active: retain the existing integer text and accuracy styling.
2. Rounding inactive, preference enabled, exact rational provenance available, and repetend at most six digits: display the complete non-repeating prefix and one overlined repetend.
3. Otherwise: use the selected ordinary numeric policy. Fixed-place mode uses its configured limit. No-truncation mode uses the available full numeric precision when compact cycle notation is disabled or unavailable.

The fixed decimal-place setting remains stored. It controls fixed-place mode. It does not truncate the prefix or repetend of an exact compact value in no-truncation mode. Settings helper text must explain this precedence.

For compact presentation, an exact integer has no decimal point or zero tail. A finite decimal has no overline and uses the ordinary numeric policy. A zero value remains different from absent frame information.

Alternative: truncate the cycle to the selected decimal-place count. Rejected because an overline would then assert a different number.

### 2a. Keep rounding, fixed places, and full precision as explicit modes

Persist the frame display mode as `round`, `decimal-places`, or `full-precision`. Map the main-window rounding control to the same mode state. Turning rounding off from `round` selects full precision to preserve the existing main-window behavior; turning it off while another mode is active preserves that mode. Enable the repeating-decimal checkbox only in `full-precision`, where it controls compact cycle notation.

### 3. Format from authoritative typed inputs

Use a shared host-neutral formatter that returns sign, integer digits, non-repeating digits, repetend digits, plain numeric text, and accessible description. Keep localized labels in host resources.

For a timestamp-derived frame value with an authoritative rational rate `p/q`, use `StartTicks * p / (TimeSpan.TicksPerSecond * q)`. Reduce the fraction with integer arithmetic. Use `BigInteger` where multiplication can exceed the current integer range.

Record exact rates through their authoritative option code or numerator/denominator metadata. For example, `Fps23976` identifies `24000/1001`. Do not infer that fraction from a rounded decimal, a localized display label, or a tolerance match from `FindByValue`.

Use long division and repeated remainders to find the shortest period. Preserve the non-repeating prefix. Keep the sign separate, including negative values between minus one and zero.

Use at most 512 long-division steps per distinct input. If the period is longer than six digits or the bound is reached, return ordinary numeric presentation. Cache results by immutable value, rate basis, and display policy where virtualization causes repeated requests. Do not cache by chapter name or row position.

When a stored frame value has no reliable timestamp/rate provenance, use its original numeric presentation. Do not invent a cycle from a repeated substring in a finite decimal string.

Alternative: scan `FramesInfo` for repeated text. Rejected because rounding tails and fixed-precision decimals do not prove an infinite repetition.

### 3a. Keep authoritative frame calculations rational end to end

When a timestamp has an authoritative rate `p/q`, calculate its frame value from `StartTicks * p / (TimeSpan.TicksPerSecond * q)`. Keep the numerator and denominator exact through display rounding, fixed-place formatting, and frame-accuracy comparison. Use `BigInteger` for products and scaled tolerance comparisons that can exceed fixed-width integers.

Expression preview must use each typed before and candidate timestamp and its own rational rate. It must not convert ticks to decimal seconds before multiplying by a decimal FPS value. An explicitly selected supported rate may supply its rational metadata when the typed snapshot has no rate. An approximate or unknown rate keeps the current numeric path and cannot establish a recurring cycle.

This decision improves arithmetic precision without changing expression meaning, tolerance values, rounding mode, candidate equality, or exported content. Decimal text remains a presentation result. It is not an input to candidate comparison when typed timestamps and rates are available.

### 4. Preserve each side's value and rate basis

Project the committed baseline and candidate from their own typed values and frame-rate basis. Apply the same formatter options to both. Never recompute the baseline using candidate timestamps or candidate frame metadata.

Keep existing frame-change and candidate-equality decisions independent of display strings. A presentation toggle must not rerun an expression or invalidate an otherwise current candidate.

At `24000/1001 fps`, the selected `t + 0.0002` example must render:

- `00:00:52.094`: `1249.(006993)` → `1249.0(117882)`.
- `00:03:38.301`: `5233.(990009)` → `5233.9(948051)`.
- `00:06:48.408`: `9792` → `9792.0(047952)`.

Parentheses identify the overlined digits in this document only. Product cells render a continuous overline and no parentheses. Neither side is converted to an integer merely because the other side has an integer value.

Alternative: reuse the already-rounded candidate frame string. Rejected because it can reproduce the incorrect mixed integer/decimal comparison from the first screenshot.

### 5. Use structured numeric layout

Use the existing semantic monospace font. Reserve independent integer, decimal-point, and fractional areas for each side. Right-align integer digits and left-align fractional digits. Reserve blank fractional space for exact integers.

Give the two comparison areas equal width and keep the arrow in its own narrow area. Size shared fractional space from the visible values and font metrics. Do not insert spaces into numeric text to simulate alignment.

With no frame change, center one formatted value in the existing frame cell. With a frame change, retain the existing before → after composition. Keep the short header “帧”. On narrow surfaces, use the existing two-line comparison or table scrolling.

Draw one thin continuous line only above repetend glyphs. Reserve enough top space to avoid clipping. Do not use combining overline characters as the underlying text. Preserve the existing neutral color for unrounded values and the existing per-side semantic styling for rounded values.

Alternative: superscripts or an ellipsis alone. Rejected because they reduce readability or fail to identify the exact repeated block.

### 6. Keep plain values available

Tooltips or existing accessible details must expose the plain numeric expansion, its displayed precision, and the exact fraction when available. Accessible text must identify the side, prefix, and repetend without depending on the line or color.

Entering a frame-cell edit must use the existing plain numeric editor value. Leaving an edit unchanged must preserve the original value exactly. Ordinary copy must copy plain numeric text, without overline glyphs, parentheses, or ellipses.

Keep display parts out of domain fields, candidate comparisons, history snapshots, and serializers. Do not change QPFile or other export input to use compact text.

## Risks / Trade-offs

- [Finite decimal rates resemble rational standards] → Require authoritative rate provenance; otherwise retain ordinary text.
- [A six-digit display limit omits longer cycles] → Use the existing numeric fallback and expose details. Never overline a truncated cycle.
- [Existing decimal-place preferences conflict with an exact cycle] → Define the precedence in Settings helper text and tests.
- [Font metrics clip the overline] → Verify font enlargement, both themes, and default, wide, and narrow surfaces.
- [Formatting changes hide a small real difference] → Preserve typed difference detection and full-value details.
- [Presentation changes leak into editing or export] → Keep a separate display model and verify unchanged edit round trips and export equality.

## Migration Plan

Add an optional boolean to the existing version-one application settings shape. Missing values load as enabled. Loading alone must not rewrite desktop or browser storage. Explicit `false` must survive normalization and Save.

Deploy the formatter and both host renderers together. Keep all original plain text available for fallback. Disabling the preference restores ordinary numeric presentation. A previous version can ignore the additive field without changing chapter content.

## Open Questions

None. Default enabled, Settings-only placement, six-digit repetend limit, existing numeric fallback, and host-specific settings lifecycles are the planning decisions for this change.
