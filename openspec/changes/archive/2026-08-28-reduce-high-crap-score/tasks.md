## 1. OpenSpec and baseline
- [x] 1.1 Validate the change and record current focused test baseline.

## 2. Core catalogs and HDMV
- [x] 2.1 Refactor `ChapterImportFormats.Code` and `ChapterExportFormats.Description` using shared definitions.
- [x] 2.2 Refactor `HdmvNavigationResolver.ExecuteSet` and `ExecuteSetSystem` with operation helpers/tables.
- [x] 2.3 Add or update Core regression tests for catalogs and navigation.

## 3. CLI workflows
- [x] 3.1 Decompose CLI validation and import input boundary helpers.
- [x] 3.2 Add or update CLI behavior tests for invalid requests, cancellation, and export output.

## 4. Avalonia UI
- [x] 4.1 Refactor `LogEntryViewModel.TryNormalizeScalar`.
- [x] 4.2 Refactor `ExpressionEditor` completion and pointer handlers.
- [x] 4.3 Refactor `MainView` key and cell-edit handlers.
- [x] 4.4 Refactor `MainWindowViewModel.OpenRelatedMediaAsync`.
- [x] 4.5 Run/update Avalonia unit and Headless behavior tests.

## 5. Infrastructure
- [x] 5.1 Refactor Windows registry probing; BDMV and composition remain for a follow-up pass.
- [x] 5.2 Add/update Infrastructure regression tests.

## 6. Verification and documentation
- [x] 6.1 Update affected code-map entries.
- [x] 6.2 Run focused tests and full solution tests.
- [x] 6.3 Validate OpenSpec artifacts and summarize remaining complexity risks.

## 7. Remaining high-complexity methods (second pass)
- [x] 7.1 Refactor `ChapterToolCliApplication.ConvertAsync` to a linear orchestration.
- [x] 7.2 Refactor `ChapterToolCliApplication.TryValidateRequest` and `WriteExportOutputAsync`.
- [x] 7.3 Refactor `ChapterToolCliApplication.ImportAsync` fallback path.
- [x] 7.4 Refactor `ChapterToolCliApplication` constructor defaults and `InspectAsync`.
- [x] 7.5 Refactor `MainView.CommitCellEditAsync` cell-edit command mapping.
- [x] 7.6 Refactor `ExpressionEditor.HandleCompletionKeys` and `OnCompletionListPointerPressed`.
- [x] 7.7 Refactor `BdmvImporter.ResolveBdjo` BDJO read/evidence helpers.
- [x] 7.8 Refactor `ChapterToolRuntimeComposition.CreateImporterRegistry` default service resolution.
- [x] 7.9 Refactor `HdmvNavigationResolver.ExecuteSetSystem` and `TryApplySetOperation` with operation tables.
- [x] 7.10 Add or update regression tests for the refactored second-pass methods.
- [x] 7.11 Run focused and full solution tests; re-measure Crap Scores.
