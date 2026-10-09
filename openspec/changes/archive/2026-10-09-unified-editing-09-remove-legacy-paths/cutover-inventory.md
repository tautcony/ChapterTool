# Interactive content cutover inventory

This inventory maps active host mutation paths to the Core session and behavior coverage.
Import, stateless conversion, and serializer consumers remain at their existing boundaries.

| Entry point | Session transaction | Behavior coverage |
| --- | --- | --- |
| Avalonia cell edit | `ClipEditingCoordinator.Edit` prepares a stable-ID candidate. `ApplyCandidateAsync` commits it to `ContentSession`. | `MainWindowViewModelTests`; `MainWindowInteractionHeadlessTests` |
| Avalonia insert, duplicate, delete, reorder, offsets, naming, expression, and FPS changes | `ClipEditingCoordinator.ExecuteCandidateAsync` commits one `ChapterContentPreview`. | `ChapterContentCandidateBuilderTests`; `MainWindowViewModelTests`; `MainWindowInteractionHeadlessTests` |
| Avalonia frame display refresh | `CommitNonStructuralChapterSetResult` converts a same-shape frame result to a candidate and commits it. It rejects row-count changes. | `ChapterWorkspaceTests`; `MainWindowViewModelTests` |
| Avalonia merge, boundary split, and append | `ChapterWorkspace.ToggleClipStructure` and `AppendClipSource` commit candidates to the same `ContentSession`. | `ChapterClipCandidateBuilderTests`; `ChapterWorkspaceTests`; `MainWindowViewModelTests` |
| Browser row edit, insert, duplicate, delete, frame shift, FPS change, naming, and expression | `WasmWorkspace` calls `ChapterWorkspace.ExecuteTrackCandidate`; it commits one selected-track candidate. | `WasmWorkspaceTests` |
| Browser merge, boundary split, and append | `WasmWorkspace` routes operations through `ChapterWorkspace` clip transactions. | `WasmWorkspaceTests`; `ChapterWorkspaceTests` |
| Table row refresh | Avalonia `WorkspaceContentRows` and Wasm `RefreshDisplay` materialize the selected track from the current session document. Frame display is temporary. | `MainWindowViewModelTests`; `WasmWorkspaceTests`; Headless interaction tests |
| Interactive preview and export | Hosts capture the committed `ChapterSet` compatibility view and serialize it with transform flags disabled. | `WasmWorkspaceTests`; `MainWindowViewModelTests`; `ChapterExportServiceTests` |
| CLI and Node.js legacy conversion | Stateless adapters transform once, then call pure serialization. They do not create an interactive session. | `ChapterToolCliApplicationTests`; `ChapterExportServiceTests`; package `core-api.test.ts` |

`ChapterWorkspace.CurrentChapterSet` is a temporary selected-track view. `ChapterImportEntry.ChapterSet` is import output used to construct the session root. Neither is a second editable workspace store.

The session history stores the root document and reversible deltas. Undo and redo use those in-memory values and do not reopen source files.
