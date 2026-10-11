## Context

`ChapterSet` stores `double FramesPerSecond` and optional `ExactFrameRate`. Editable documents and segments also have parallel rate fields. `EditableChapterDocumentAdapter.FromLegacyFrameRate` scales a double to an integer ratio. `WithFallbackFrameRate` can turn a detected rate into document metadata.

`ExactFrameMath` already calculates with integer ratios for rational and decimal inputs. Some exporters and compatibility services still calculate from `TimeSpan.TotalSeconds`. Node DTOs expose FPS and times as JavaScript numbers. Existing Node consumers change those returned fields directly. `packages/chaptertool/src/types.ts` contains handwritten contracts; the package build emits declarations from those sources.

The user confirmed gradual compatibility only for manual numeric input and established external APIs. Internal types and call chains must change directly. Catalog selections must carry rationals. The user also confirmed exact CLI ratio input, explicit computation-rate selection for unknown or mixed rates, and a separate `tool.exact` Node facade.

## Goals / Non-Goals

**Goals:**

- Store one canonical rate value per current or source rate.
- Preserve ticks and rate provenance through every supported typed workflow.
- Keep established external signatures, result fields, and Node object mutation workflows usable.
- Remove internal numeric rate signatures and parallel compatibility state.
- Make rate selection, suggestions, mixed-rate handling, failures, and rounding reviewable.
- Keep source metadata unchanged after current-content transformations.
- Give C#, CLI, and Node callers an explicit exact input path.

**Non-Goals:**

- Change tick storage or introduce persistent document history.
- Add arbitrary-precision Lua arithmetic, SMPTE/drop-frame notation, or a general VFR rate model.
- Extract new FPS metadata from ffprobe or change the supported catalog.
- Change established timestamp text formats, CUE's 75-unit time base, or MPLS PTS-to-time import quantization.
- Add Node session/history APIs or change the existing JSON chapter export file schema.
- Remove compatibility APIs in this change. Removal needs a separately versioned change.

## Decisions

### 1. Canonical value, validity, and equality

Add immutable `FrameRateValue`. Its states are `Rational(ChapterFrameRate)`, `Approximate(decimal)`, and `Unknown`. The default value must be Unknown. A rational must have positive signed-64-bit components and must reduce by GCD. An approximate value must be positive and representable as a decimal.

`default(ChapterFrameRate)` is currently possible despite constructor checks. All public factories, converters, serializers, and operation boundaries must reject its zero components. Invalid input must not become Unknown. Constructors can throw argument exceptions; Try factories and workflow boundaries must return diagnostics.

Equality and hashing must include kind and normalized payload. `48000/2002` equals `24000/1001`. Approximate `24.0` equals approximate `24`, but differs from rational `24/1`. Labels and tolerant catalog matching must not determine equality.

Internal frame-rate options must carry a canonical rate directly. Catalog selection must pass its rational value, without numeric matching or parsing a label. Numeric catalog values can remain computed projections only on retained external contracts. Auto and Reserved have no usable computation rate. Numeric lookup and timestamp detection return suggestions, not source authority.

An alternative that stores numeric FPS plus an optional exact value leaves conflicting states possible. It is rejected.

### 2. Import, aggregation, and source ownership

Each editable segment must carry current `FrameRateValue` and original `SourceFrameRate`. Original source rate includes explicit Unknown. Missing source metadata must not be filled from a later current rate. Remove parallel `ExactFrameRate` members from internal documents and segments. Keep derived aliases only where an established external contract needs them.

Track and document summaries must inspect every participating segment. A summary is known only when all contributors are known and equal by kind and payload. Known plus Unknown is Unknown. Exact plus numerically equal Approximate is not a uniform authoritative rate. No first-segment, first-video-stream, or non-null-only fallback is allowed. A track without segments uses its explicit content rate. Selected-track projections must not borrow an unrelated track's document summary.

Use these importer rules:

| Source | Canonical rate policy |
| --- | --- |
| MPLS/BDMV | Resolve the relevant primary video rate per play item. Preserve recognized catalog rationals. Missing or conflicting primary-video metadata produces Unknown and a diagnostic where appropriate. |
| DVD IFO | Preserve the existing authoritative NTSC `30000/1001` or PAL `25/1` interpretation. NAV timing changes timestamp evidence, not rate authority. |
| XPL | Parse timeBase/tickBase without a double arithmetic path. Those clock units and the current hard-coded 24 FPS do not establish video FPS. Report Unknown unless actual video-rate metadata establishes a rate. |
| Text/XML/VTT/CUE/media chapter input | Keep Unknown when no video rate is reported. A supplied decimal video rate is Approximate. CUE's 75 units per second and media time_base are timestamp scales, not video FPS. |

The [Toshiba playback-system patent, description of FIG. 23B](https://patents.google.com/patent/US20070086102A1/en) identifies XPL timeBase as a title-timeline frequency and tickBase as an independently configured page/application clock. The domain policy follows from that distinction: neither clock alone proves the encoded video's frame rate. Preserve the existing XPL timestamp interpretation in this change.

MPLS split entries retain their own rates. Aggregate playlists must retain per-item rate/boundary metadata for document adaptation. Add immutable metadata to the existing import projection when necessary; do not add another editable chapter backup.

Scalar `ChapterSet` compatibility operations cannot represent a mixed timeline. They must report a conflict when flattening would discard required per-segment rate information. Document operations can preserve mixed segments. Merge and append must not reinterpret ticks using the first rate. Existing merge conflict policy remains the default; this change does not add a piecewise frame-number transform.

### 3. Computation context and Auto suggestions

Resolve an operation rate in this order: explicit caller-selected rate, then a uniform known current rate for the target scope. Missing or mixed scope needs explicit selection. A known approximate source rate remains approximate under Auto; it must not be replaced by the closest catalog rational.

Auto can calculate suggested frame display with confidence. The suggestion is separate from content and operation inputs. It must not change source/current metadata, document equality, history, export baselines, or the rate used by a content operation. An empty detector result remains a low-confidence suggestion.

Selecting a valid catalog entry explicitly supplies that entry's rational rate for the requested calculation. It does not itself create content history. A reviewed FPS conversion commits its target as the current rate. Ordinary frame edits and export overrides use the selected calculation rate without rewriting original source metadata.

Add a typed optional computation-rate override to serialization options for frame-number formats. Preserve the old options constructor. An override affects captured output only. Unknown-rate time-only serialization remains valid. A valid v2 timecode map supplies chapter-to-QPFile frame mapping without requiring a constant FPS. Invalid provided FPS still fails validation; it must not be silently replaced by the map.

Approximate custom rates and exact rates outside the catalog need a stable custom selector entry on load. Both hosts must preserve its kind and value on reselection, track switching, and language changes.

Implicit fallback detection is rejected because it hides the source uncertainty.

### 4. Arithmetic and rounding boundaries

Use `ExactFrameMath` or equivalent BigInteger products for ticks, frame counts, and both rate kinds. A decimal can be decomposed into its exact coefficient and scale for arithmetic. This does not change Approximate provenance. Do not require decimal multiplication if it can overflow before the final result is checked.

All four source/target pairs must work: rational/rational, rational/approximate, approximate/rational, and approximate/approximate. No approximate target may force an exact source through a decimal or double.

Use these boundaries:

| Operation | Rounding |
| --- | --- |
| Frame cell value to ticks | Keep the entered integer or finite decimal frame count exact until nearest tick; midpoint away from zero. |
| FPS conversion | Round source frame position once to an integer, midpoint away from zero; convert that frame to target ticks with the same tick rule. |
| Segment timeline conversion | Convert absolute start and end with the chapter rule; duration is converted end minus converted start. |
| QPFile/celltimes without timecode map | Convert ticks to integer frames once, midpoint away from zero. |
| Display and repeating decimals | Round only presentation. Never parse an unchanged rounded display back into content. |
| Timecode map and timestamp text | Keep their existing mapping/format policy. They must not mutate canonical ticks or rate metadata. |

Core calculations must not use `TotalSeconds`, a display number, or a compatibility FPS projection as input. Check final signed-64-bit bounds, end-before-start, and segment collapse. Overflow must return a diagnostic and reject the whole candidate. Keep separators excluded from content arithmetic.

An explicit ChangeFps action keeps its source-frame quantization rule even when numeric rates are equal. It is unchanged only when the resulting ticks and canonical metadata equal the base snapshot. A rate metadata assignment without a timing transform must preserve ticks. Off-grid conversion is not generally reversible; Undo must restore the exact prior snapshot.

### 5. Candidates, history, and output state

Rate comparison must include canonical kind and payload in documents, segments, property diffs, history deltas, and export fingerprints. Equivalent reduced rationals must not create false changes. Exact-to-approximate changes must not disappear merely because a displayed FPS matches.

Capture operation rates with the candidate. Apply must commit the reviewed candidate once. A display preference change can refresh its presentation but must preserve the candidate. Changing an operation-rate draft invalidates the corresponding prepared candidate. Track switching, content changes, and history navigation retain existing stale-candidate rules.

Undo, redo, branch restoration, merge, split, and append must preserve current and source rates separately. Unknown source rates must remain Unknown through repeated conversions. Export preview and save must use the same captured ticks, typed override, and rounding policy. Display preferences and Auto suggestions must not enter the content fingerprint.

### 6. External-only compatibility and numeric normalization

Compatibility is an input/output boundary, not an internal migration layer.

| Consumer | Migration policy |
| --- | --- |
| Core documents, segments, calculations, history, host state, and internal service interfaces | Replace numeric and parallel-rate contracts directly. Do not retain old overloads for repository callers. |
| UI catalog and operation selectors | Pass canonical rationals directly. No double or decimal compatibility path is needed for selection. |
| User-entered numeric FPS and existing numeric CLI options | Accept the existing input form once and classify it as Approximate. |
| Established external Core/Node inputs and returned data | Retain large disruptive signatures/shapes as deprecated boundary adapters or projections. New exact interfaces use canonical values. |

Inventory the documented and distributed external API surface before editing. A C# `public` modifier alone does not establish a compatibility obligation. Preserve established numeric API signatures and result fields through major version 23. Public model construction/deconstruction/with usage needs preservation only where it belongs to that external surface. Remove internal-only legacy adapters, parallel source/exact fields, and numeric overloads.

Legacy positive numeric input means Approximate even for `24`, `23.976`, or `24000d/1001d`. Normalize doubles once from invariant shortest round-trip text. Accept only a positive decimal representation without clamping, underflow-to-zero, or additional decimal rounding. Decimal inputs retain their exact coefficient and scale subject to normalization.

Legacy metadata FPS `0` represents Unknown. Zero supplied to a required computation-rate parameter is invalid. Negative, non-finite, out-of-range, or malformed supplied values fail. Explicit rational metadata and catalog selection must be preserved directly.

The user confirmed one compatibility exception during implementation: invalid numeric
`ChapterSet` construction or `FramesPerSecond` init assignment throws an argument
exception immediately. It no longer creates an invalid model for a later export or
expression diagnostic. Try factories and operation request boundaries must still
return structured diagnostics. A valid canonical model must not carry deferred
numeric errors or an invalid rate state.

Retained external `FramesPerSecond` getters derive a numeric projection from canonical data; Unknown projects to the old zero sentinel. A retained numeric init/update is an explicit Approximate assignment and must clear stale exact authority. Canonical construction must bypass numeric adapters. Retained `ExactFrameRate` aliases derive from the current rational, not immutable source metadata. Test ordering only for retained external construction paths.

Migrate expression contexts and internal interfaces directly. If the API inventory proves an established external expression-engine extension contract, keep its adapter at the external boundary. Do not retain every internal interface or context signature speculatively.

### 7. Node exact facade and wire schema

Keep existing external `ChapterTool` methods, DTO field names, numeric seconds/FPS, and legacy object mutation behavior. Internal JSExport bindings can change with their wrappers. Do not add higher-priority canonical fields to old DTOs. Add `tool.exact` and typed wire bindings for import, chapter edits, shifts, zones, combine/append, detection/frame projection, FPS conversion, expressions, pure serialization, one-shot export, and conversion tools. Non-content catalogs can reuse shared endpoints. Both facades must call the same canonical Core services.

The exact rate wire union is:

```ts
type FrameRateDto =
  | { kind: "rational"; numerator: string; denominator: string }
  | { kind: "approximate"; value: string }
  | { kind: "unknown" };
```

Rational components use positive invariant integer strings within Int64 range. Approximate values use invariant decimal strings, without exponent, grouping, or locale-specific punctuation. Output must be reduced/canonical. Invalid kinds, missing required payloads, extra state payloads, zero denominators, overflow, and rounded-to-fit decimals fail at the boundary.

Exact chapter-set DTOs use `frameRate`, nullable `durationTicks`, chapter `startTicks`, and nullable `endTicks`. Tick values are signed Int64 strings and follow the existing document validity rules. A null duration is Unknown; a string zero duration is known zero. Exact operations accept rate DTOs or explicit catalog codes, not JavaScript numbers. No FPS double or seconds number is canonical in this facade. Keep seconds convenience projections out of the exact input contract to avoid conflicting representations.

Adapt exact requests to typed single-track document snapshots without creating a session. Preserve `ChapterDuration` availability and ticks directly. Do not route exact requests through a numeric legacy DTO or its zero-duration sentinel. Retain precision through name/structure operations as well as FPS-dependent operations.

Stateless scalar exact combine/append must reject mixed-rate flattening. They do not introduce a document/session API. App document paths retain segment metadata separately.

The existing Node facade remains an intentionally lossy compatibility boundary. Reimporting its numeric output cannot promise authoritative provenance or full tick precision. Document that limitation and show exact-facade migration examples. JavaScript number components were rejected because Int64 values can exceed the safe-integer range.

Update `NodeJsonContext` source-generation registrations, handwritten `types.ts`, `index.ts`, emitted `dist/index.d.ts` through the package build, and actual packaged-runtime tests together. Do not edit generated declarations by hand.

### 8. CLI input and output

Add `--frame-rate-ratio N/D` through DotMake.CommandLine. Accept positive Int64 numerator/denominator with optional surrounding whitespace. Reduce the result. Reject malformed, zero, negative, or out-of-range components. Reject simultaneous numeric and ratio options before import or file creation. Migrate the internal `CliConvertRequest` directly to a canonical rate. Compatibility applies to the user-facing option syntax.

An explicit rate overrides computation for this one-shot conversion, including FPS-dependent expression preparation. It does not retime chapters unless an expression explicitly does so. With neither option, use source rate; do not infer from Auto.

Inspect remains terminal text. Keep its current identifiers and numeric FPS projection for compatibility. Add a rate-kind label and canonical ratio/decimal or Unknown text. Do not invent a CLI JSON result mode in this change. `--stdout` emits only exported content. Validation exits with code 1, not the unhandled-exception code.

### 9. Lua boundary

Keep numeric `fps` and `chapter.fps` for a known explicitly resolved rate. Add `fpsRateKind` and exact ratio text globals `fpsNumeratorText`/`fpsDenominatorText`. The proposed numeric `fpsNumerator`/`fpsDenominator` are available only when both integers are safely representable by the Lua numeric domain; otherwise they are nil. Approximate and Unknown expose no rational pair.

Unknown context uses nil for `fps` and `chapter.fps`. Time-only expressions such as `t + 1` can run. Built-in FPS-dependent presets require an explicit rate. Custom Lua arithmetic that uses an absent rate fails structurally; do not decide dependencies by scanning raw source text. A script can explicitly handle missing data.

Construct seconds from ticks before the Lua projection. If Lua returns the same numeric time it received, preserve the original ticks. Changed results return through one validated tick conversion. Recompute derived frame values from resulting ticks with the canonical rate. Update evaluation, analysis/completion context, symbol metadata, presets, and help together. Raw Lua arithmetic remains floating point, even when ratio text is available.

## Risks / Trade-offs

- [Legacy API remains lossy] -> Keep its shape stable and document the exact migration path. Typed APIs carry the precision guarantee.
- [Established external records have more surface than FPS parameters] -> Inventory real contract usage and compile/run those consumers. Do not preserve unrelated internal signatures.
- [Mixed timelines lack one global frame grid] -> Preserve segment data in documents and require explicit scalar computation rates. Reject unsupported scalar flattening.
- [Auto no longer authorizes unknown-rate edits] -> Keep suggested display and localized feedback. Make manual rate selection available in both hosts.
- [Importer clocks resemble FPS] -> Test IFO authority, MPLS item aggregation, XPL clock handling, and CUE/media Unknown classification separately.
- [Long timelines overflow intermediate arithmetic] -> Use integer products and check final bounds. Verify failures leave content and output untouched.

## Migration Plan

1. Add value, normalization, and wire contracts with focused tests.
2. Add necessary external adapters and directly replace internal signatures and models.
3. Migrate import, segment/document adaptation, comparison/history, and fingerprints.
4. Migrate calculations, expressions, serializers, and complete host workflows.
5. Add the exact Node facade and CLI ratio input. Keep existing entry points.
6. Verify installed/packed consumers and publish migration examples.

No persisted document-history format needs migration. Rollback can remove the additive exact facade and CLI option while retaining the prior public surface. Do not persist new rate selections in settings without a separate requirement.

## Precision Regression Evidence

`ExactFrameRateWorkflowTests` must run 500 reversible cycles for all four Rational/Approximate source and target combinations.
Each cycle converts the rate, multiplies time by 1001/1000, divides by the same factor, and converts back.
The test must compare the result with the original ticks after every cycle.
An independent integer comparison must establish a loss below one source frame.
The timelines include off-grid ticks and `9007199254740993` ticks.
The test does not establish reversibility for arbitrary lossy transforms or arbitrary Lua scripts.
Lua remains floating-point arithmetic.
MPLS and IFO fixture regressions must also cover fractional frame edits, candidate isolation, history, both conversion directions, and export equality.

## Open Questions

No product decision remains open. Implementation must inventory retained public signatures and verify wire registrations against the compiled and packaged consumers. This inventory must not reopen the confirmed compatibility or Auto policies.
