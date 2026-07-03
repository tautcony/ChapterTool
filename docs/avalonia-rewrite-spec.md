# ChapterTool Avalonia 重寫功能規格

本文件用於在不依賴 WinForms UI 代碼的前提下重建 ChapterTool。它是 Avalonia 重寫的主 spec 與索引：主文檔記錄全局產品契約、架構邊界與驗收清單；每個子功能的細節以 `docs/modules/*.md` 為準；文件級覆蓋以 `docs/coverage-matrix.md` 為準。

## 0. 子文檔索引、覆蓋與驗證狀態

本輪文檔已按功能模塊拆分，並使用獨立 subagent 先產出、再用另一批 subagent 驗證。Avalonia 重寫時應先讀本主 spec，再按涉及功能讀對應子文檔。

| 模塊 | 子文檔 | 主責範圍 |
| --- | --- | --- |
| 01 | `docs/modules/01-ui-shell-and-interactions.md` | 主窗口 UI、命令、快捷鍵、表格交互、語言切換入口、窗口狀態 |
| 02 | `docs/modules/02-core-model-transform-export.md` | 核心模型、時間/幀率/表達式、章節編輯、七類導出 |
| 03 | `docs/modules/03-text-xml-matroska-vtt-importers.md` | OGM/TXT、XML、Matroska、WebVTT importer |
| 04 | `docs/modules/04-disc-playlist-media-importers.md` | MPLS、IFO/DVD、XPL、BDMV/eac3to、MP4/libmp4v2 importer |
| 05 | `docs/modules/05-cue-flac-tak-importers.md` | CUE、FLAC embedded CUE、TAK embedded CUE importer 與 CUE exporter |
| 06 | `docs/modules/06-supporting-ui-platform-services.md` | 輔助窗口、日誌、通知、配置、registry、資源、更新、平台服務、`ToolKits` |
| 07 | `docs/modules/07-tests-build-distribution-assets.md` | 測試、樣例、csproj/sln、CI、NSIS、發佈資產、根文件 |

覆蓋規則：

- `docs/coverage-matrix.md` 列出每個源文件、測試文件、資產、根文件的 primary module 和 referenced modules。
- 機械覆蓋校驗已覆蓋 `rg --files Time_Shift Time_Shift_Test dist .github` 以及根文件 `Time_Shift.sln`、`README.md`、`ChangeLog.md`、`LICENSE`、`.gitattributes`、`.gitignore`、`bump-version.pl`。
- `ToolKits.cs` 的 primary module 是 06；時間/編碼/保存語義也在 02/03/05 交叉引用。
- `SharpDvdInfo/LICENSE` 同時影響 04 的 DVD importer 授權與 07 的發佈/資產覆蓋。

二輪驗證已修正的關鍵點：

- `RegistryStorage` 主要是程序目錄下 `chaptertool.json`，不是直接 Windows registry；真正 registry 用於 .NET release 檢查、CPU/MKVToolNix 探測、`.mpls` 文件關聯與 NSIS 安裝卸載項。
- 表達式 `and/or/xor` 是殘留/不完整支持，不能作為 Avalonia 版已支持契約，除非先補測與修正。
- `Time2String` 使用 `Math.Round` 預設 midpoint-to-even，毫秒 1000 只進位到 seconds 欄位，存在 `:60.000` 邊界風險。
- OGM/XML 等 parser 對缺失/無效時間常會默認 `TimeSpan.Zero`，不是所有格式錯誤都會拋出專用異常。
- BDMV 現有 `TaskAsync` 只返回 stdout，不返回 stderr/exit code，也沒有 timeout/cancellation。
- CUE/FLAC/TAK importer 沒有外部進程依賴；FLAC unknown/reserved metadata block 和 malformed Vorbis comment 會透出一般異常。
- 現有測試不是全量驗收：VTT 和 CueSharp 測試是 smoke test，CUE 自動斷言主要使用 `ARCHIVES 2.cue`。
- release 版本來源分裂：`bump-version.pl` 更新 assembly version，但不更新 NSIS `PROG_VERSION`。

## 1. 功能捕獲機制

重寫前需要先把舊程序拆成三層契約，避免新 UI 直接照搬事件處理器。

1. **功能契約層**
   - 記錄每個用戶命令的入口、前置條件、狀態改變、輸出與錯誤行為。
   - 本文件第 3 到第 9 節即為第一版功能契約。

2. **核心服務層**
   - 將舊代碼中的 `ChapterInfoGroup`、`ChapterInfo`、`Chapter`、解析器、導出器、表達式與幀率計算抽為 UI 無關服務。
   - Avalonia UI 不應直接修改控件狀態來推導業務狀態；應通過 ViewModel 狀態驅動。

3. **驗收樣例層**
   - 使用 `Time_Shift_Test` 中現有樣例與單元測試作為格式驗收基線。
   - 每個輸入格式至少覆蓋：成功載入、章節列表、幀數顯示、重命名/刪除/插入、導出主要格式。
   - 對外部工具依賴功能，需提供“依賴缺失”的可測行為。

重寫期間每新增或調整功能，都應先更新本文件，再改核心服務和 Avalonia UI。

## 2. 現有應用概覽

ChapterTool 是章節提取、校正、編輯與導出工具。主窗口以一個章節表格為核心，配合載入、保存、預覽、片段選擇、幀率/取整、章節序號平移、時間表達式、章節名模板與日誌窗口。

主要舊代碼來源：

- `Time_Shift/Forms/Form1.cs`：主窗口狀態、命令、快捷鍵、載入/保存/表格交互。
- `Time_Shift/Forms/Form1.Designer.cs`、`Form1.resx`、`Form1.en-US.resx`：控件、菜單與可見文字。
- `Time_Shift/Util/ChapterInfo.cs`、`Chapter.cs`：章節模型與導出邏輯。
- `Time_Shift/Util/ChapterData/*Data.cs`：各格式解析器。
- `Time_Shift/Util/Expression.cs`：時間轉換表達式。
- `Time_Shift/Forms/FormPreview.cs`、`FormLog.cs`、`FormColor.cs`：輔助窗口。

## 3. 數據模型與應用狀態

### 3.1 核心模型

- `Chapter`
  - `Number`：顯示與導出的章節序號。
  - `Time`：章節時間戳，內部用 `TimeSpan`。
  - `Name`：章節名。
  - `FramesInfo`：當前幀率與取整策略下的幀數顯示，例如 `1234 K` 或 `1234 *`。

- `ChapterInfo`
  - `Title`：章節標題或來源標題。
  - `SourceName`：對應視頻、片段或播放列表名。
  - `SourceIndex`：BDMV/eac3to 等來源索引。
  - `SourceType`：`OGM`、`XML`、`MPLS`、`DVD`、`HD-DVD`、`CUE`、`WebVTT` 等。
  - `FramesPerSecond`：當前來源或推斷幀率。
  - `Duration`：片段總時長。
  - `Chapters`：章節列表。
  - `Expr`：當前時間變換表達式；默認為 `t`。
  - `Tag`/`TagType`：原始數據或解析上下文。

- `ChapterInfoGroup`
  - 一組可選片段/Edition/Title。
  - 子類用於區分來源：`MplsGroup`、`IfoGroup`、`XplGroup`、`XmlGroup`、`BDMVGroup`。

### 3.2 主窗口狀態

Avalonia ViewModel 至少需要以下狀態：

- `CurrentPath`：當前文件或 BDMV 目錄。
- `IsDirectorySource`：來源是否為 BDMV 目錄。
- `InfoGroup`：可選片段集合。
- `CurrentInfo`：當前顯示與編輯的章節。
- `SelectedClipIndex`：當前片段索引；無選擇時按 0 處理。
- `CombineChapter`：MPLS/IFO 是否合併所有片段。
- `AutoGenName`：導出與顯示時是否使用 `Chapter 01` 形式的自動章節名。
- `RoundFrames`：是否對幀數四捨五入。
- `FrameTolerance`：幀數接近整數的容差，默認 `0.15`，可選 `0.01`、`0.05`、`0.10`、`0.15`、`0.20`、`0.25`、`0.30`。
- `SelectedFrameRateIndex`：幀率下拉選擇。
- `OrderShift`：章節序號平移量，舊 UI 範圍 `0..50`。
- `ApplyExpression`：是否啟用時間表達式。
- `ExpressionText`：時間表達式文本。
- `ExpressionMode`：中綴或逆波蘭表達式。
- `ChapterNameTemplate`：從文本文件載入的章節名模板。
- `SaveType`：導出格式。
- `XmlLanguage`：XML 導出語言。
- `CustomSavingPath`：右鍵保存按鈕設置的自定義輸出目錄。
- `StatusText`、`Progress`：狀態欄文本與進度。
- `LogText`：程序日誌。

## 4. 主窗口 UI 契約

### 4.1 第一屏常駐控件

- `Load` 按鈕
  - 左鍵：打開文件選擇器並載入支持格式。
  - 右鍵菜單：
    - `Reload file`：重新載入當前文件或 BDMV 目錄。
    - `Append file`：當前來源為 MPLS 時，可追加另一個 `.mpls`。

- `Save` 按鈕
  - 左鍵：以當前導出格式保存。
  - 右鍵：選擇並記住自定義輸出目錄。
  - 鼠標提示：對只有兩個章節點且疑似無效的 MPLS 片段提示“可能是無用章節”。

- 路徑標籤
  - 顯示當前文件名；長文件名中間省略。
  - 懸停顯示完整路徑。

- 轉換/刷新按鈕
  - 顯示 `↺` 或 `↻`。
  - 左鍵：刷新章節表格。
  - 右鍵：打開配色窗口。

- 幀率下拉框
  - 用於非 MPLS/IFO 等未知幀率來源。
  - 既有項對應 `MplsData.FrameRate` 中有效幀率：`24000/1001`、`24`、`25`、`30000/1001`、`50`、`60000/1001`，另有 Auto/佔位行為由舊下拉索引決定。
  - 對已知幀率來源（MPLS、DVD）禁用。

- `Round` 勾選框
  - 開啟時幀數取整並顯示可信標記。
  - 右鍵/上下文菜單可選容差。

- 片段下拉框
  - 只有多片段、多 Edition 或多 Title 來源時顯示/啟用。
  - 每項格式大致為 `{SourceName}__{ChapterCount}` 或 `Edition NN`。
  - 右鍵菜單：
    - `Merge chapter`：MPLS/IFO 來源可切換合併章節。
    - 對 MPLS/IFO/XPL，動態追加“打開對應視頻文件”菜單項。

- 章節表格
  - 列：`#`、`Time Stamp`、`Chapter Name`、`Frames`。
  - `#` 只讀。
  - 可編輯時間、章節名與幀數。
  - 右鍵行菜單：
    - `Create Zones`
    - `Forward translation`
    - `Insert new Chapter`

- `Preview` 按鈕
  - 左鍵：顯示 OGM/TXT 格式預覽。
  - 右鍵：在 Windows 下嘗試以管理員權限註冊 `.mpls` 文件關聯。

- 狀態欄
  - 顯示狀態文字、進度條、展開/收起按鈕。
  - 點擊展開/收起按鈕顯示高級面板。
  - 舊程序中連續點擊進度條可打開 About 窗口，此行為可保留為隱藏彩蛋，但不是核心重寫要求。

### 4.2 高級面板

高級面板默認收起，展開後包含：

- `Apply expr (t)`：啟用時間表達式。
- 表達式下拉/輸入框：舊資源內置至少兩個表達式樣例。
- 逆波蘭表達式勾選：切換表達式解析方式。
- `Shift Order` 數值框：章節序號整體後移。
- `Auto name`：顯示/導出時忽略原始章節名，使用 `Chapter NN`。
- `Use Template`：從文本文件載入章節名模板並套用。
- `Type`：保存格式下拉。
- `Lang`：XML 語言下拉；僅保存格式為 XML 時啟用。
- `LOG`：打開日誌窗口。

## 5. 快捷鍵契約

- `Ctrl+O`：載入文件。
- `Ctrl+S`：保存當前格式。
- `Alt+S`：保存當前格式後切換到下一保存格式；到最後一項時再保存一次。
- `Ctrl+R` 或 `F5`：刷新表格。
- `Ctrl+L`：打開日誌窗口。
- `PageDown` / `PageUp`：切換下一個/上一個片段。
- `Ctrl+0..9`：切換到片段 0..9；舊邏輯中 `Ctrl+1` 對應第一項，`Ctrl+0` 對應第十項。
- `Alt+0..9`：切換保存格式。
- `Ctrl+PageUp` / `Ctrl+PageDown`：切換表達式下拉項。
- `F11`：展開或收起高級面板。

## 6. 載入格式契約

### 6.1 支持的輸入

- 章節文件：`.txt`、`.xml`、`.mpls`、`.ifo`、`.xpl`
- CUE/音頻：`.cue`、`.tak`、`.flac`
- Matroska：`.mkv`、`.mka`
- MP4：`.mp4`、`.m4a`、`.m4v`
- WebVTT：`.vtt`
- BDMV 目錄：拖放目錄，且存在 `BDMV/PLAYLIST`

文件載入入口：

- 文件選擇器。
- 拖放文件。
- 啟動參數直接傳入文件路徑。
- 拖放 BDMV 目錄。

### 6.2 各格式行為

- OGM/TXT
  - 解析 `CHAPTERNN=hh:mm:ss.sss` 與 `CHAPTERNNNAME=name`。
  - 第一個章節時間作為初始時間，所有章節時間減去初始時間。
  - 允許空行；遇到後續解析錯誤時若已解析章節，返回已解析部分並記錄中斷日誌。

- XML/Matroska XML
  - 根節點必須是 `Chapters`，子節點為 `EditionEntry`。
  - 支持多 Edition。
  - 支持 `ChapterTimeStart`、`ChapterTimeEnd`、`ChapterDisplay/ChapterString`。
  - 有限支持嵌套 `ChapterAtom`。
  - 移除相鄰相同時間的冗餘章節。

- MPLS
  - 解析 Blu-ray `.mpls`。
  - 每個片段生成一個 `ChapterInfo`，片段項顯示 `{SourceName}__{ChapterCount}`。
  - 記錄片段時長、章節數、來源文件名與幀率。
  - 可合併所有片段為一個章節列表。
  - 可追加另一個 `.mpls` 到當前 `MplsGroup`。

- IFO/DVD
  - 從 DVD IFO 中讀取 Title/PGC 章節。
  - 支持多段顯示與合併。
  - 章節序號在載入後重排。
  - DVD 幀率根據 PAL/NTSC 推導。

- XPL/HD-DVD
  - 讀取 HDDVD playlist XML 命名空間 `http://www.dvdforum.org/2005/HDDVDVideo/Playlist`。
  - 從 `TitleSet/Title/ChapterList/Chapter` 讀取章節。
  - 使用 `timeBase`、`tickBase`、`tickBaseDivisor` 解析時間。

- CUE/FLAC/TAK
  - `.cue` 直接解析。
  - `.flac`/`.tak` 支持讀取內嵌 CUE，再轉換為章節。

- Matroska
  - 通過 `mkvextract.exe chapters "{path}"` 讀取 XML，再走 XML 解析。
  - 優先使用已保存路徑，其次查找 MKVToolNix 安裝位置，最後嘗試程序目錄。
  - 缺少 `mkvextract.exe` 時報錯並記錄路徑。

- MP4
  - 依賴 `libmp4v2.dll` 和 Knuckleball 讀取 MP4 章節。
  - 缺少 DLL 時提示並可跳轉下載頁。
  - 舊程序為避免路徑問題會在磁盤根目錄建立臨時硬鏈接再讀取。

- WebVTT
  - 文件必須包含 `WEBVTT`。
  - 以空行切分 cue。
  - 每個 cue 找到 `-->` 時間行，下一行作為章節名，開始時間作為章節時間。

- BDMV 目錄
  - 拖放目錄觸發。
  - 需要 `eac3to.exe`。若未配置或路徑失效，提示用戶輸入並保存。
  - 從 `BDMV/META/DL/*.xml` 讀取碟片標題。
  - 調用 eac3to 列出播放列表，篩出含章節的項，再導出 `chapters.txt` 並用 OGM 解析。
  - 路徑過於複雜或含非 ASCII 可能導致 eac3to 報錯，應保留明確錯誤提示。

## 7. 編輯與轉換契約

### 7.1 表格刷新

刷新時先更新幀數，再更新表格行。

- MPLS/DVD：
  - 使用來源已知幀率。
  - 幀率下拉禁用。

- 其他來源：
  - 若 `RoundFrames=true` 且未手動指定幀率，根據章節時間在候選幀率中自動檢測。
  - 若 `RoundFrames=false`，使用下拉框當前幀率。

進度值：

- 載入或未完成狀態通常為 `33`。
- 表格有多行後為 `66`。
- 保存成功後為 `100`。
- 保存失敗後為 `60`。

### 7.2 幀數計算

候選幀率表：

- index 0：`0`，自動檢測時跳過。
- index 1：`24000/1001`
- index 2：`24`
- index 3：`25`
- index 4：`30000/1001`
- index 5：`0`，自動檢測時跳過。
- index 6：`50`
- index 7：`60000/1001`

幀數公式：

```text
frames = Expr.Eval(chapter.Time.TotalSeconds, CurrentInfo.FramesPerSecond) * selectedFrameRate
```

`RoundFrames=true` 時：

- 顯示四捨五入後的幀號。
- 若原始幀數與整數差小於容差，後綴 `K`。
- 否則後綴 `*`。

`RoundFrames=false` 時：

- 顯示未取整幀數。

### 7.3 表達式

用途：將章節時間 `t` 轉換為新時間。

支持：

- 中綴表達式。
- 逆波蘭表達式。
- 變量：`t`、`fps`。
- 常量：`M_E`、`M_PI` 等 C math 常量。
- 函數：`abs`、`acos`、`asin`、`atan`、`atan2`、`cos`、`sin`、`tan`、`cosh`、`sinh`、`tanh`、`exp`、`log`、`log10`、`sqrt`、`ceil`、`floor`、`round`、`rand`、`dup`、`int`、`sign`、`pow`、`max`、`min`。
- 運算符：`+`、`-`、`*`、`/`、`%`、`^`、`>`、`<`、`>=`、`<=`、`and`、`or`、`xor`。
- `//` 後作為註釋。

輸入校驗：

- 僅允許數字、字母、下劃線、空白、基本運算符、括號、逗號、點與註釋。
- 括號必須平衡。
- 不允許形如 `1abc` 的變量。

### 7.4 章節序號與章節名

- `OrderShift` 會將章節序號重排為 `1 + shift`、`2 + shift`...
- `AutoGenName=true` 時表格顯示和導出使用 `Chapter NN`，不修改原始 `Name`。
- `Use Template=true` 時打開文本文件，逐行套用到章節名；模板不足時保留原章節名。
- 刪除第一行後，剩餘章節時間會以新的第一章時間為 0 重新平移。
- 對 MPLS/IFO，若沒有章節名模板，刪除第一行後重新生成章節名。

### 7.5 表格編輯

- 編輯 `Time Stamp`
  - 用 `TimeSpan.TryParse` 解析。
  - 大於 1 天視為非法，重置為 `00:00:00.000`。
  - 編輯後刷新幀數。

- 編輯 `Chapter Name`
  - 更新當前 `Chapter.Name`。
  - 記錄重命名日誌。

- 編輯 `Frames`
  - 從輸入中提取第一個整數。
  - 用當前幀率轉換為時間。
  - 編輯後刷新。

- 刪除行
  - 從 `CurrentInfo.Chapters` 刪除對應 `Chapter`。
  - 重排序號。
  - 若刪除第一行，時間整體向前平移。

- 插入新章節
  - 只在選中一行時有效。
  - 在選中行前插入 `Name="New Chapter"`、`Time=0`、`Number=0` 的章節。
  - 插入後重排序號並刷新。

### 7.6 行右鍵工具

- `Create Zones`
  - 對選中行生成 x264/x265 `--zones` 參數。
  - 每個 zone 起點為當前行幀號，終點為下一行幀號減 1。
  - 最後一行目前使用自身幀號作為退化區間。
  - 生成後詢問是否複製到剪貼板。

- `Forward translation`
  - 提示輸入需要向前平移的幀數。
  - 使用當前幀率換算為時間。
  - 所有章節時間減去該時間。
  - 平移後小於 0 的章節被刪除。

## 8. 導出契約

### 8.1 保存路徑

默認輸出目錄：

- 未設自定義路徑時，輸出到來源文件所在目錄。
- 已設自定義路徑時，輸出到該目錄。

文件名：

- 基礎名為 BDMV 標題或來源文件名。
- 對 `.mpls`、`.ifo` 追加 `__{CurrentInfo.SourceName}`。
- 後綴前追加遞增序號 `_1`、`_2`...，避免覆蓋現有文件。

### 8.2 保存格式

- `TXT`：`.txt`
  - OGM 樣式：
    - `CHAPTERNN=hh:mm:ss.sss`
    - `CHAPTERNNNAME=name`

- `XML`：`.xml`
  - Matroska Chapters XML。
  - 根節點 `Chapters/EditionEntry/ChapterAtom`。
  - 寫入 `ChapterDisplay/ChapterString`、`ChapterLanguage`、`ChapterUID`、`ChapterTimeStart`、`ChapterFlagHidden`、`ChapterFlagEnabled`。
  - 語言來自語言下拉；空值時使用 `und`。

- `QPF`：`.qpf`
  - 每行為幀號加 `I`。
  - 以 UTF-8 無 BOM 寫出。

- `TimeCodes`：`.TimeCodes.txt`
  - 每行一個章節時間戳。

- `TsmuxerMeta`：`.TsMuxeR_Meta.txt`
  - 格式：
    - `--custom-`
    - `chapters=time1;time2;...`

- `CUE`：`.cue`
  - 包含 `REM Generate By ChapterTool`。
  - `TITLE "{CurrentInfo.Title}"`
  - `FILE "{sourceFileName}" WAVE`
  - 每章節輸出 `TRACK NN AUDIO`、`TITLE`、`INDEX 01 mm:ss:ff`。

- `JSON`：`.json`
  - 字段：
    - `sourceName`：MPLS 來源時為 `{SourceName}.m2ts`，其他來源為 `null`。
    - `chapter`：`[{ "name": "...", "time": seconds }]`
  - 對 `TimeSpan.MinValue` 分隔標記有特殊處理：重置基準時間並重新從 0 輸出。

保存前會刷新當前表格；保存後應記錄保存類型、文件名、來源文件、語言、是否使用章節名、是否使用模板、序號平移、是否應用表達式。

## 9. 輔助窗口與系統功能

- 預覽窗口
  - 顯示 `CurrentInfo.GetText(AutoGenName)` 的 TXT/OGM 預覽。
  - 位置跟隨主窗口，貼在主窗口左側。
  - TopMost。
  - 關閉時隱藏而非銷毀。
  - 雙擊文本框關閉/隱藏。
  - 文本超過 20 行且較長時顯示滾動條。

- 日誌窗口
  - 顯示全局日誌。
  - 激活或刷新時更新內容。
  - 文本變化後滾動到底部。
  - 支持複製選中文本。
  - 關閉時隱藏。

- 配色窗口
  - 可修改主窗口背景、文本背景、按鈕懸停、按鈕按下、邊框、文字前景色。
  - 關閉時保存到 `color-config.json`。

- 配置與 registry 邊界
  - 舊程序的 `RegistryStorage` 名稱帶 registry，但主要配置實際寫到程序目錄下 `chaptertool.json`。
  - JSON 配置鍵名仍使用類似 `Software\ChapterTool.SavingPath`、`Software\ChapterTool.Location`、`Language`、`Software\ChapterTool.mkvToolnixPath`、`eac3toPath`。
  - 真正的 Windows registry 用途包括 .NET Framework release key 檢查、MKVToolNix 安裝位置探測、`.mpls` 文件關聯，以及安裝器 uninstall/InstallLocation 項。
  - Avalonia 版應提供兼容讀取，並可遷移到用戶配置目錄下的強類型配置結構；registry 相關功能必須隔離為 Windows-only 平台服務。

- 語言切換
  - 舊程序在存在 `en-US/ChapterTool.resources.dll` 時，系統菜單可切換語言並重啟。
  - Avalonia 版應用資源系統重新設計，但至少保留中/英界面能力。

- 文件關聯
  - 右鍵預覽可註冊 `.mpls` 由 ChapterTool 打開。
  - 需要管理員權限。
  - Avalonia 版應把此行為做成顯式設置項，而不是隱藏在預覽按鈕右鍵。

## 10. 外部依賴

- .NET Framework 4.8 是舊版本要求；Avalonia 版應改為現代 .NET。
- Matroska 章節提取依賴 MKVToolNix 的 `mkvextract.exe`。
- MP4 章節讀取依賴 `libmp4v2.dll`。
- BDMV 章節讀取依賴 `eac3to.exe`。
- Windows 專屬能力：
  - 文件關聯。
  - 管理員檢測/提權。
  - 部分註冊表兼容查找。
  - MP4 臨時硬鏈接策略。

Avalonia 重寫若要跨平台，應將外部工具定位、進程執行、文件關聯、提權、配置存儲抽成平台服務。

## 11. Avalonia 重構建議架構

### 11.1 建議項目邊界

- `ChapterTool.Core`
  - 模型：`Chapter`、`ChapterInfo`、`ChapterInfoGroup`。
  - 解析器接口：`IChapterImporter`。
  - 導出器接口：`IChapterExporter`.
  - 表達式、時間、幀率、章節名、語言表。

- `ChapterTool.Infrastructure`
  - 文件系統、配置、外部進程、剪貼板、文件關聯、平台檢測。
  - MKVToolNix/eac3to/libmp4v2 適配器。

- `ChapterTool.Avalonia`
  - Views、ViewModels、命令、對話框服務、資源。
  - 不直接引用 WinForms。

- `ChapterTool.Tests`
  - 保留並遷移現有測試。
  - 增加 ViewModel 命令測試與導出快照測試。

### 11.2 ViewModel 命令映射

- `LoadFileCommand`
- `LoadDirectoryCommand`
- `ReloadCommand`
- `AppendMplsCommand`
- `SaveCommand`
- `ChooseSaveDirectoryCommand`
- `RefreshCommand`
- `SelectClipCommand`
- `ToggleCombineCommand`
- `SetFrameRateCommand`
- `SetToleranceCommand`
- `ToggleRoundCommand`
- `ToggleAutoNameCommand`
- `LoadChapterNameTemplateCommand`
- `ToggleExpressionCommand`
- `SetExpressionCommand`
- `EditChapterTimeCommand`
- `EditChapterNameCommand`
- `EditChapterFrameCommand`
- `DeleteChapterCommand`
- `InsertChapterCommand`
- `CreateZonesCommand`
- `ForwardTranslateCommand`
- `OpenPreviewCommand`
- `OpenLogCommand`
- `OpenColorSettingsCommand`
- `RegisterMplsAssociationCommand`

### 11.3 重寫時必須消除的舊耦合

- 表格行不應直接保存 `Chapter` 作為 UI Tag；應使用可觀察集合與選中項。
- 幀數計算不應依賴 ComboBox 索引；應使用明確的 `FrameRateOption`。
- 保存格式不應依賴 ComboBox SelectedIndex；應使用枚舉。
- 錯誤提示、日誌、狀態欄更新應由命令結果統一返回。
- 外部工具路徑不應散落在解析器內；應由依賴服務提供。
- BDMV 載入不應在核心解析器內直接彈輸入框；應返回缺少依賴的錯誤，由 UI 決定如何詢問。

## 12. 重寫驗收清單

### 12.1 基本流程

- 啟動無參數時顯示空狀態。
- 啟動帶文件路徑時自動載入。
- 拖放支持格式文件時載入。
- 拖放 BDMV 目錄時進入 BDMV 載入流程。
- 無效路徑或不支持格式不會殘留舊章節狀態。

### 12.2 格式驗收

- `Time_Shift_Test/[VTT_Sample]/chapter.vtt` 可載入並顯示章節。
- `Time_Shift_Test/[mpls_Sample]/*.mpls` 可載入、切換片段、合併、導出。
- `Time_Shift_Test/[ifo_Sample]/VTS_05_0.IFO` 可載入 DVD 章節。
- `Time_Shift_Test/[cue_Sample]/*.cue` 可載入 CUE。
- `Time_Shift_Test/[ogm_Sample]/*.txt` 可載入 OGM。
- XML/Matroska XML 測試需覆蓋多 Edition 與嵌套 ChapterAtom。

### 12.3 編輯驗收

- 修改時間後幀數立即更新。
- 修改幀號後時間立即更新。
- 修改章節名後保存與預覽反映新名稱。
- 刪除第一章會把剩餘章節平移到 0 起點。
- 插入章節後序號重排。
- `Auto name` 不破壞原始章節名。
- 章節名模板可重複套用到切換後片段。

### 12.4 幀率與表達式驗收

- 自動幀率檢測會跳過無效幀率項。
- 容差變更會影響 `K`/`*` 標記。
- 關閉取整時顯示原始幀數。
- 中綴與逆波蘭表達式都可解析並作用於顯示和導出時間。
- 非法表達式會顯示錯誤狀態，且不導致崩潰。

### 12.5 導出驗收

- 七種保存格式均可生成文件。
- 文件名自動避重名。
- 自定義保存目錄可持久化。
- XML 語言選擇生效；未指定語言時使用 `und`。
- QPF 使用 UTF-8 無 BOM。
- 保存前會同步表格最新編輯。

### 12.6 輔助功能驗收

- 預覽窗口可更新、跟隨主窗口、關閉後可再次打開。
- 日誌窗口可刷新、複製、關閉後再次打開。
- 配色可保存並下次啟動載入。
- 快捷鍵與菜單命令可達成舊版同等操作。

## 13. 已知風險與重寫決策點

- Changelog 中很多歷史行為只存在於事件處理與 bugfix 中，應優先用測試固定現有輸入輸出。
- `ChangeLog.md` 歷史格式不規整、部分記錄不完整，不應作為唯一需求來源。
- 舊程序有若干隱藏交互（進度條點擊 About、預覽右鍵文件關聯、轉換鍵右鍵配色），Avalonia 版可改成更顯式入口，但需記錄兼容決策。
- `Expression.EvalCMathTwoToken` 中 `xor` 目前兩側都讀取 `value.Number`，疑似舊 bug；重寫時若修正，需增加兼容說明和測試。
- `ChapterInfo.Chapter2Qpfile` 中時間轉幀邏輯疑似遺留且未被主 UI 調用；除非有外部入口需求，不應作為主重寫範圍。
- MP4 與 BDMV 依賴 Windows/外部二進制，跨平台支持需要先定義依賴安裝與錯誤模型。
