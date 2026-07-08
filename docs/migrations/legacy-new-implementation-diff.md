# src 與 Time_Shift 新舊實現差異分析

日期：2026-06-12

範圍：

- 舊 WinForms 實現：`Time_Shift/`
- 新 .NET/Avalonia 實現：`src/ChapterTool.Core`、`src/ChapterTool.Infrastructure`、`src/ChapterTool.Avalonia`

本報告只做只讀分析，未修改實現，也未執行測試。分析按模塊拆分並並行核對：章節導入、導出/轉換/編輯、UI 工作流、平台/設置/外部工具。

## 模塊地圖

| 模塊 | 舊實現 | 新實現 |
| --- | --- | --- |
| 章節導入 | `Time_Shift/Util/ChapterData/*`、`Time_Shift/Knuckleball`、`Time_Shift/SharpDvdInfo` | `src/ChapterTool.Core/Importing`、`src/ChapterTool.Infrastructure/Importing`、`RuntimeChapterImporterRegistry` |
| 導出/輸出投影 | `Time_Shift/Util/ChapterInfo.cs`、`Time_Shift/Forms/Form1.cs` | `ChapterExportService`、`ChapterOutputProjectionService` |
| 時間/幀率/表達式/編輯 | `ToolKits.cs`、`Expression.cs`、`ChapterInfo.cs`、`Form1.cs` | `src/ChapterTool.Core/Transform`、`src/ChapterTool.Core/Editing`、`MainWindowViewModel` |
| UI 工作流 | `Form1`、`FormPreview`、`FormLog`、`FormColor`、WinForms 控件 | `MainWindow.axaml`、工具窗、ViewModels、Avalonia services |
| 平台/設置/外部工具 | 註冊表、Win32 P/Invoke、自更新、靜態日誌 | JSON 設置、平台服務、外部工具 locator、Serilog/ILogger |
| 打包/原生依賴 | .NET Framework WinExe、Fody/Costura、`libmp4v2.dll` | .NET 10 Avalonia、Assets、ATL.NET/ffprobe、退役 Fody/Costura |

## 總體結論

新實現不是舊 WinForms 的一比一移植，而是將核心能力拆分為 Core/Infrastructure/Avalonia 三層。常規章節導入、常規導出、基礎編輯、片段合併、zones、前移幀、預覽/日誌等主流程大體已遷移；新版還擴展了 ffprobe 通用媒體導入和 WebVTT 核心導出。

仍然存在舊功能未實現或行為不等價的地方。高優先級差異包括：

- XML `ChapterTimeEnd` 導入語義不同。
- MP4 無章節 fallback 行為不同。
- `celltimes`、`Chapter2Qpfile`、`ChangeFps` 未見等價能力。
- 舊表達式語法未完全兼容。
- WebVTT 導出核心已存在，但 UI 未暴露。
- 新增媒體格式在打開文件主過濾器中未完整顯示。
- 旧快捷鍵語義部分變更。
- 文件關聯、自更新、提權等 Windows 專屬功能目前被禁用或占位。

## 差異清單

| 功能/模塊 | 新實現情況 | 差異/缺失 | 效果 |
| --- | --- | --- | --- |
| 導入格式覆蓋 | 舊格式基本都有：txt/xml/vtt/cue/flac/tak/mpls/ifo/xpl/mkv/mka/mp4/m4a/m4v/BDMV | 新增大量 ffprobe 媒體格式：webm/mks/mov/qt/3gp/asf/mp3/ogg/wav 等 | 覆蓋面總體增強 |
| 打開文件過濾器 | Runtime registry 支持很多格式 | Avalonia 文件選擇器主過濾只列舊核心格式，新增格式需要使用 All files | 功能存在但可發現性不足 |
| XML 導入 | 新解析 `ChapterTimeEnd` 到 `Chapter.End` | 舊版會把 `ChapterTimeEnd` 額外生成一個同名結束章節；新版不再顯示/導出為獨立章節 | 某些 XML 導入後章節數會減少 |
| MP4 導入 | 新優先 ffprobe，ffprobe 不可用時 ATL fallback | 舊 `libmp4v2` 在無章節時生成單個 `Chapter 1`；新版無章節直接失敗 | 無章 MP4 的占位章節行為消失 |
| BDMV 導入 | 新用 `eac3to -showall` 列候選，再直接讀 MPLS | 舊版會讓 eac3to 實際導出 `chapters.txt` 再按 OGM 解析 | 對 eac3to 輸出格式更敏感，結果可能不完全一致 |
| Matroska 導入 | 新 `mkvextract` 主路徑，失敗 fallback ffprobe | registry 分派 `.mks/.webm`，但 `MatroskaChapterImporter.SupportedExtensions` 只聲明 `.mkv/.mka` | 元數據不一致，可能影響測試/展示 |
| CUE 編碼 | 新 UTF-8 嚴格解碼 | 舊無 BOM 時非嚴格 UTF-8 解碼 | 非 UTF-8 本地編碼 `.cue` 更容易失敗 |
| WebVTT 導入 | 新有明確 importer | 帶 cue timing settings 的行會顯式報不支持 | 失敗更清楚，但兼容面仍有限 |
| 導出格式 | 新覆蓋 TXT/XML/QPF/TimeCodes/TsMuxer/CUE/JSON，並新增 WebVTT 核心導出 | UI 保存格式和 TextTool 格式列表未暴露 WebVTT | 核心能力存在，用户入口不完整 |
| Celltimes | 新未見等價導出 | 舊 `GetCelltimes()` 可生成 celltimes | 舊工作流缺失 |
| Chapter2Qpfile | 新未見等價工具 | 舊 `Chapter2Qpfile()` 可從章節文本轉 QPF，支持 timecode 文件 | 高級轉換工具缺失 |
| XML 導出 | 新固定 `EditionUID=1`、`ChapterUID=chapter.Number`、單行輸出 | 舊有 XML declaration、doctype 注釋、隨機 UID、格式化輸出 | 嚴格比較和部分下游腳本可能受影響 |
| BOM/編碼 | 新有 `EmitBom` 選項痕跡 | 保存服務實際 `File.WriteAllTextAsync`，未按舊 QPF 無 BOM/其他默認 BOM 語義處理 | 輸出字節級兼容性不同 |
| 時間舍入 | 新 formatter 使用 ToEven 舍入 | 舊 `Math.Round` 默認行為不同 | 邊界毫秒可能不一致 |
| ChangeFps | 新未見等價能力 | 舊 `ChapterInfo.ChangeFps()` 可按原幀數不變重算時間/時長 | 幀率變換類工作流缺失 |
| 表達式語法 | 新支持中綴/後綴、函數、比較、三元 `?:` | 舊支持 `rand/dup/and/or/xor`，新版不支持；新版增加三元 | 舊表達式可能失敗或結果不同 |
| 表格幀號預覽 | 新幀信息基於原始時間；表達式主要在輸出投影/導出階段應用 | 舊表格幀號也應用 `_info.Expr` | 啟用表達式時 UI 預覽和舊版不同 |
| 負序號偏移 | 新會規範化會產生非正編號的負偏移 | 舊直接應用 | 新更安全，但舊邊緣輸出不兼容 |
| UI 核心工作流 | 載入、保存、預覽、日誌、片段、合併、插入、刪除、zones、前移幀基本保留 | 佈局和入口重組 | 主流程可用 |
| 快捷鍵 | 新保留 `Ctrl+O/S/R/L`、`F5`、`PageUp/Down`、`Ctrl/Alt+數字` 等 | 舊 `F11` 展開面板，新為預覽；舊 `Alt+S` 連存並推進片段，新為保存目錄；舊 `Ctrl+PageUp/Down` 切表達式模板缺失 | 老用户鍵盤流會受影響 |
| 預覽窗 | 新是普通工具窗，復用 TextTool | 舊是貼靠主窗左側、置頂、無邊框、雙擊隱藏 | 快速對照體驗下降 |
| XML 語言選擇 | 新主窗只有 `und/zh/ja/en` | 舊有完整 ISO 語言列表 | 小語種 XML 輸出選擇能力下降 |
| 配色 | 新用十六進制文本槽位保存 | 舊點擊色塊打開系統 ColorDialog 且即時應用 | 新交互更間接 |
| 文件關聯 | 新 UI 有提示/占位，服務返回 unsupported | 舊能寫 HKCR 註冊 `.mpls` 關聯並提權 | 刻意禁用高風險 Windows 功能，但對舊用户是功能缺失 |
| 自更新 | 新無自更新/周檢查 | 舊有 `Updater` + `FormUpdater` 下載替換 exe，雖然自動周檢查看起來已註釋 | 新 UI 內不能檢查/安裝更新 |
| 管理員/Win32 輔助 | 新提權服務占位，不做 runas | 舊有管理員檢測、runas、UAC 盾牌、進度條狀態、硬鏈接等 P/Invoke | Windows 專屬體驗被移除或未遷移 |
| 設置遷移 | 新 JSON 設置並遷移部分舊 `chaptertool.json`/`color-config.json` | 舊統計、`LastCheck`、`DoVersionCheck` 等未遷移 | 舊統計/更新狀態丟失 |
| 日誌 | 新 UI 最近日誌 + 文件日誌，結構化 | 舊是無限靜態內存文本 | 新更工程化，但文本日誌行為不等價 |

## 按模塊細節

### 章節導入

已基本遷移：

- OGM/TXT、Matroska XML、WebVTT。
- CUE、FLAC 內嵌 CUE、TAK 內嵌 CUE。
- MPLS、IFO/DVD、XPL、BDMV。
- MKV/MKA、MP4/M4A/M4V。

新版增強：

- Matroska 增加 `.mks/.webm` 分派。
- MP4/媒體導入擴展為 ffprobe 通用媒體章節導入，支持 `.mov/.qt/.3gp/.asf/.mp3/.ogg/.wav/.ffmeta` 等。
- Matroska 失敗時可 fallback 到 ffprobe。
- MP4 在 ffprobe 缺失/無法啟動時可 fallback 到 ATL。

主要差異：

- XML `ChapterTimeEnd` 不再額外生成同名結束章節。
- MP4 無章節不再生成單個 `Chapter 1`。
- BDMV 從 eac3to 導出 chapters.txt 改為 eac3to 列候選 + 直接讀 MPLS。
- CUE 對非 UTF-8 編碼更嚴格。
- 新增媒體格式在 UI 主過濾器中未完整列出。

### 導出、轉換、編輯

已基本遷移：

- TXT/OGM、XML、QPF、TimeCodes、tsMuxeR meta、CUE、JSON。
- 自動章節名、模板章節名、章節序號偏移。
- 時間/幀編輯、刪除、插入、前移幀、zones。
- DVD/MPLS combine、MPLS append。

新版增強：

- Core 層有 WebVTT 導出。
- 導出前投影集中處理表達式、模板名和序號偏移。
- 編輯服務和分段服務從 UI 事件中抽離，測試邊界更清楚。

主要差異：

- 缺少 `GetCelltimes()` 對應導出。
- 缺少 `Chapter2Qpfile()` 對應工具。
- 缺少 `ChangeFps(decimal fps)` 等價行為。
- 表達式語法不完全兼容舊版。
- 幀號預覽是否應用表達式與舊版不同。
- XML/BOM/時間舍入存在字節級或邊界差異。

### UI 工作流

已基本遷移：

- 主窗口載入、保存、刷新、預覽、日誌、設置。
- 片段選擇、合併、追加 MPLS。
- 表格編輯、插入、刪除、打開相關媒體、生成 zones、前移幀。
- `Ctrl+O/S/R/L`、`F5`、`PageUp/Down`、`Ctrl+數字`、`Alt+數字`、`Insert/Delete` 等快捷鍵。

主要差異：

- 舊貼邊置頂預覽窗改為普通工具窗。
- `F11`、`Alt+S` 語義變更。
- `Ctrl+PageUp/PageDown` 切表達式模板缺失。
- XML 語言列表從完整 ISO 列表縮減為常用值。
- 配色從系統 ColorDialog 即時選色改為十六進制文本槽位。
- 文件關聯入口變為未啟用提示。

### 平台、設置、外部工具

新版增強：

- 設置從註冊表/本地 json 混用改為 `appsettings.json`、`theme-colors.json`。
- 支持從舊 `chaptertool.json`、`color-config.json` 遷移部分字段。
- `ExternalToolLocator` 統一定位 `mkvextract`、`eac3to`、`ffprobe`。
- `ProcessRunner` 支持 `ArgumentList`、UTF-8 stdout/stderr、退出碼、超時、取消、kill 進程樹。
- 日誌從靜態內存文本升級為 UI 日誌面板 + 文件日誌。

主要差異：

- 無自更新下載器和周更新檢查。
- 文件關聯、提權為占位/unsupported。
- 舊統計類註冊表鍵未遷移。
- Win32 UI 輔助能力未遷移，例如進度條狀態色、前台窗口、DPI P/Invoke、硬鏈接命令。
- `libmp4v2.dll`、Fody/Costura 被退役，這部分看起來是新架構的刻意選擇。

## 優先處理建議

| 優先級 | 項目 | 建議 |
| --- | --- | --- |
| 高 | XML `ChapterTimeEnd` | 明確產品語義：要保留新版 `End` 模型，還是兼容舊版生成結束章節。若保留新版，補文檔和導出/顯示策略。 |
| 高 | MP4 無章節 fallback | 決定是否恢復單個 `Chapter 1` 占位行為，避免舊用户導入無章 MP4 時體驗倒退。 |
| 高 | 缺失工具 | 評估是否補 `celltimes`、`Chapter2Qpfile`、`ChangeFps`。這三項是明確的舊能力缺口。 |
| 高 | 表達式兼容 | 若要兼容舊工程/舊用户習慣，需要補 `rand/dup/and/or/xor` 或提供遷移提示。 |
| 中 | WebVTT 導出入口 | Core 已有能力，補 UI 保存格式和 TextTool 格式列表成本較低。 |
| 中 | 打開文件過濾器 | 將 registry 支持的新增媒體格式同步到 Avalonia 文件選擇器主過濾器。 |
| 中 | 快捷鍵兼容 | 明確 `F11`、`Alt+S`、`Ctrl+PageUp/PageDown` 是否要恢復舊語義或提供替代。 |
| 中 | XML 語言列表 | 若目標是舊版完整能力，恢復完整 ISO 語言選擇。 |
| 低/產品決策 | 文件關聯、提權、自更新 | 這些是高風險 Windows 專屬能力。建議明確標記為“刻意移除”或用 Windows-gated 實現補回。 |

## 主要證據入口

- 舊主窗口功能分發：`Time_Shift/Forms/Form1.cs`
- 舊模型與導出：`Time_Shift/Util/ChapterInfo.cs`
- 舊導入器：`Time_Shift/Util/ChapterData/*`
- 舊 MP4 native wrapper：`Time_Shift/Knuckleball/MP4File.cs`
- 舊平台/註冊表/提權：`Time_Shift/Util/ToolKits.cs`
- 舊更新：`Time_Shift/Util/Updater.cs`、`Time_Shift/Forms/FormUpdater.cs`
- 新導入分派：`src/ChapterTool.Avalonia/Services/RuntimeChapterImporterRegistry.cs`
- 新導出：`src/ChapterTool.Core/Exporting/ChapterExportService.cs`
- 新輸出投影：`src/ChapterTool.Core/Exporting/ChapterOutputProjectionService.cs`
- 新轉換/表達式：`src/ChapterTool.Core/Transform`
- 新編輯/分段：`src/ChapterTool.Core/Editing`
- 新主窗口 VM：`src/ChapterTool.Avalonia/ViewModels/MainWindowViewModel.cs`
- 新工具窗 VM：`src/ChapterTool.Avalonia/ViewModels/ToolWindowViewModels.cs`
- 新設置與平台服務：`src/ChapterTool.Infrastructure/Configuration`、`src/ChapterTool.Infrastructure/Platform`
- 新外部工具與進程：`src/ChapterTool.Infrastructure/Tools`、`src/ChapterTool.Infrastructure/Processes`
