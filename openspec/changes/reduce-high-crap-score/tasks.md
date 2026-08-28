## 1. OpenSpec and baseline
- [x] 1.1 Validate the change and record current focused test baseline.

## 2. Core catalogs and HDMV
- [x] 2.1 Refactor `ChapterImportFormats.Code` and `ChapterExportFormats.Description` using shared definitions.
- [x] 2.2 Refactor `HdmvNavigationResolver.ExecuteSet` and `ExecuteSetSystem` with operation helpers/tables.
- [x] 2.3 Add or update Core regression tests for catalogs and navigation.

## 3. CLI workflows
- [x] 3.1 Decompose CLI validation and import input boundary helpers.
- [ ] 3.2 Add or update CLI behavior tests for invalid requests, cancellation, and export output.

## 4. Avalonia UI
- [x] 4.1 Refactor `LogEntryViewModel.TryNormalizeScalar`.
- [ ] 4.2 Refactor `ExpressionEditor` completion and pointer handlers.
- [ ] 4.3 Refactor `MainView` key and cell-edit handlers.
- [x] 4.4 Refactor `MainWindowViewModel.OpenRelatedMediaAsync`.
- [ ] 4.5 Run/update Avalonia unit and Headless behavior tests.

## 5. Infrastructure
- [x] 5.1 Refactor Windows registry probing; BDMV and composition remain for a follow-up pass.
- [ ] 5.2 Add/update Infrastructure regression tests.

## 6. Verification and documentation
- [ ] 6.1 Update affected code-map entries.
- [x] 6.2 Run focused tests and full solution tests.
- [x] 6.3 Validate OpenSpec artifacts and summarize remaining complexity risks.
