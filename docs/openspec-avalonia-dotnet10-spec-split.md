# Avalonia + .NET 10 OpenSpec 拆分總表

本文件把 `docs/avalonia-rewrite-spec.md` 與 `docs/modules/*.md` 轉換為 OpenSpec change `rewrite-avalonia-dotnet10` 的 capability 邊界。拆分原則是：先以 SDD 固定需求契約，再在每個 capability 內以 TDD 建立測試基線，最後按依賴順序 apply。

## 拆分總覽

| Spec | 來源文檔 | 責任邊界 | 主要依賴 | 可並行 apply |
| --- | --- | --- | --- | --- |
| `chapter-core-transform-export` | `02-core-model-transform-export.md` | Core 模型、章節群組、時間/幀率、表達式、章節編輯、命名、zones、七種 exporter | 無，第一批先行 | 可與 solution scaffolding 並行；其接口穩定後解除 importer/UI 阻塞 |
| `tests-build-distribution-assets` | `07-tests-build-distribution-assets.md`、`coverage-matrix.md` | .NET 10 solution、測試工程、fixtures、CI、包裝、版本、資產與授權 | 無，第一批先行 | 可與 core 並行 |
| `supporting-ui-platform-services` | `06-supporting-ui-platform-services.md` | 設定、日誌、對話框、剪貼板、進程、工具定位、原生依賴、Windows-only 服務、輔助窗口 | solution scaffolding；部分接口需與 importer/UI 對齊 | 可與 importer 純解析工作並行 |
| `chapter-importers-text-xml-matroska-vtt` | `03-text-xml-matroska-vtt-importers.md` | OGM/TXT、WebVTT、XML/Matroska XML、Matroska/mkvextract adapter | Core import result/diagnostic；Matroska 依賴 process/tool locator | OGM、VTT、XML 可並行；Matroska adapter 等 XML/process contract |
| `cue-flac-tak-import-export` | `05-cue-flac-tak-importers.md` | CUE importer、FLAC/TAK embedded CUE、CUE exporter | Core model、diagnostic、export contract、encoding helper | CUE parser、FLAC reader、TAK scanner、CUE exporter 可並行 |
| `disc-playlist-media-importers` | `04-disc-playlist-media-importers.md` | MPLS、IFO/DVD、XPL、BDMV/eac3to、MP4/libmp4v2、clip combine/append/source refs | Core model/diagnostic；BDMV 依賴 OGM parser + process runner；MP4 依賴 native resolver | MPLS、IFO、XPL 可並行；BDMV/MP4 等服務接口穩定 |
| `avalonia-ui-shell` | `01-ui-shell-and-interactions.md` | 主窗口、ViewModel commands、快捷鍵、拖放、表格交互、clip 選擇、context menus、狀態呈現 | Core service contracts、settings/dialog/window services；可先 mock | XAML shell/ViewModel tests 可先行；真 importer/exporter wiring 後置 |

## 執行順序

1. **Foundation**
   - `tests-build-distribution-assets`
   - `chapter-core-transform-export`
   - 產物：SDK-style solution、Core contracts、test fixture policy、diagnostic/result/export options。

2. **Shared Services**
   - `supporting-ui-platform-services`
   - 產物：settings、process runner、tool locator、dialog/window/clipboard/logging/localization abstractions。

3. **Pure Importers**
   - `chapter-importers-text-xml-matroska-vtt`
   - `cue-flac-tak-import-export`
   - `disc-playlist-media-importers` 的 MPLS/IFO/XPL 子集
   - 產物：不依賴 UI 的 importer 單元測試與 sample fixture 驗收。

4. **Dependency-backed Importers**
   - Matroska/mkvextract、BDMV/eac3to、MP4/libmp4v2
   - 產物：mock process/native adapter 測試、缺依賴診斷、外部工具取消/timeout/exit code 行為。

5. **Avalonia UI**
   - `avalonia-ui-shell`
   - 產物：ViewModel 命令測試、快捷鍵 routing、表格交互、clip selection/combine/append、輔助窗口入口與狀態呈現。

6. **End-to-End Verification**
   - 使用既有 `Time_Shift_Test` fixtures 建立兼容快照。
   - 執行 `dotnet test`、`dotnet build`、必要時 UI smoke tests。

## 每個 spec 的 TDD 基線

- 每個 capability 必須先建立失敗測試或 golden snapshot，再實作。
- Parser/importer 測試不得依賴 Avalonia 或 WinForms。
- 外部工具測試必須使用 fake process runner/tool locator，不依賴本機安裝。
- ViewModel 測試只 mock services，不直接打開文件對話框、MessageBox、剪貼板或 registry。
- CI 至少執行 restore/build/test；發佈與 installer 可在後續任務中分階段加入。

## 子代理拆分結果整合

| 子任務 | 結論 |
| --- | --- |
| UI shell | 建議 capability `avalonia-ui-shell`，重點是 ViewModel 命令面與無 WinForms 耦合。 |
| Core transform/export | 建議 capability `chapter-core-transform-export`，是最小依賴基礎層。 |
| Text/XML/Matroska/VTT | 建議 capability `chapter-importers-text-xml-matroska-vtt`，OGM/VTT/XML 可先純 Core，Matroska adapter 後接平台服務。 |
| Disc/media importers | 建議 capability `disc-playlist-media-importers`，MPLS/IFO/XPL 可純解析並行，BDMV/MP4 等依賴 adapter。 |
| CUE/FLAC/TAK | 建議 capability `cue-flac-tak-import-export`，CUE parser 和 exporter 共享模型但可並行。 |
| Supporting services | 建議 capability `supporting-ui-platform-services`，承擔輔助窗口、配置遷移、平台隔離和外部工具服務。 |

## Apply 並行策略

主代理負責 OpenSpec artifacts、接口收斂、任務狀態和最終驗證。子代理 apply 時必須使用 disjoint write set：

- Agent A：solution/test scaffolding、`Directory.Build.props`、fixture layout、CI。
- Agent B：`ChapterTool.Core` models/time/frame/expression/export contracts。
- Agent C：text/XML/VTT importers。
- Agent D：CUE/FLAC/TAK importer/exporter。
- Agent E：MPLS/IFO/XPL pure importers。
- Agent F：Infrastructure services and dependency-backed adapters。
- Agent G：Avalonia ViewModels/XAML shell after contracts stabilize。

如果某個任務需要改動共享 contracts，必須先由主代理完成或明確鎖定，再啟動依賴它的子代理。
