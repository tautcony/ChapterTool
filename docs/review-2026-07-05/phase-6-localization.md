# Phase 6 — Localization Consistency Review

> Scope: 3× `Strings.*.resx` + `LocalizationTests.cs`.
> Reviewer: sub-agent bg_0defc2e9 + main-agent verification.

## Phase Summary

**No structural i18n defects found.** All three resx files have exactly 234 keys each, with zero missing/extra keys across languages. All 62 keys containing placeholders have identical placeholder name-sets across all three languages. No duplicate keys, no XML malformation, no UTF-8 mojibake detected. The i18n refactor's structural integrity is solid.

## Files Reviewed

- `src/ChapterTool.Avalonia/Localization/Resources/Strings.en-US.resx` (719 lines)
- `src/ChapterTool.Avalonia/Localization/Resources/Strings.ja-JP.resx` (719 lines)
- `src/ChapterTool.Avalonia/Localization/Resources/Strings.zh-CN.resx` (719 lines)
- `tests/ChapterTool.Avalonia.Tests/Localization/LocalizationTests.cs` (163 lines)

## Key Parity Table

| Culture | Key Count | Missing vs en-US | Extra vs en-US | Duplicates |
|---------|-----------|-------------------|-----------------|------------|
| en-US | 234 | — | — | 0 |
| ja-JP | 234 | 0 | 0 | 0 |
| zh-CN | 234 | 0 | 0 | 0 |

**Verdict: PERFECT PARITY.**

## Placeholder Parity

- Total keys with placeholders: **62**
- Placeholder mismatches across languages: **0**
- Positional `{0}` placeholders: **0** (all use named `{name}` convention)
- Named-placeholder consistency: **100%**

Spot-checked high-risk keys: `Log.Diagnostic` (6 placeholders: code/details/location/message/operation/severity) — identical in all 3 languages. `Log.ImportOption` (9 placeholders) — identical in all 3 languages.

## Main-Agent Cross-Verification

The main agent independently verified ExpressionService diagnostic-code coverage:

```
$ comm -23 <(grep -oE 'InvalidExpression\.[A-Za-z]+' ExpressionService.cs | sort -u) \
           <(grep -oE 'name="Diagnostic\.InvalidExpression\.[A-Za-z]+"' en-US.resx | ... | sort -u)
(empty — zero missing codes)
```

All 18 `InvalidExpression.*` codes thrown by `ExpressionService.cs` have corresponding `Diagnostic.InvalidExpression.*` keys in the resx. **Coverage complete.**

## XML / UTF-8 Health

- All three files: well-formed XML ✅
- CJK spot-check (byte-level): `日本語`, `簡体字中国語`, `简体中文` — all valid UTF-8, no mojibake signatures (`Ã`, `Â`, U+FFFD) ✅
- No empty `<value></value>` where English counterpart is non-empty ✅

## Test Coverage

`LocalizationTests.cs` enforces:
- Key parity: `SupportedCulturesHaveMatchingResourceKeys()` ✅
- Placeholder parity: `LocalizedFormatStringsUseCompatiblePlaceholders()` ✅
- Encoding artifacts: `NonEnglishResourcesDoNotContainEncodingArtifacts()` ✅
- Runtime formatting: `LocalizerFallsBackAndFormatsMessages()` ✅
- New diagnostic smoke test: `DiagnosticKeysLocalizeAcrossCultures()` ✅

**No coverage gap requiring a defect ticket.**

## Findings

**None.** This phase is clean.

## 漏检复盘 (Missed-pattern Retrospective)

- 键集合不一致: 三语 234 键一致，差集为空。Clean.
- 占位符不一致: 62 键逐一比对，完全一致。Clean.
- 命名/位置占位符混用: 未发现 `{0}` 位置参数。Clean.
- 重复 key: 三文件均无重复。Clean.
- XML 结构损坏: 三文件解析通过。Clean.
- UTF-8 损坏: CJK 字节级 spot-check 正常。Clean.
- 空翻译值: 无非英文空值。Clean.
