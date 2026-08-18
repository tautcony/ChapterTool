# 模塊 05：CUE/FLAC/TAK 解析與 CUE 相關模型

## 1. 模塊目的與重寫邊界

本模塊負責把 CUE sheet 及帶有內嵌 CUE 的音頻文件轉換為核心章節模型，並描述 CUE 導出與核心導出流程的接口關係。重寫時應把解析、元數據讀取、錯誤模型與 UI 狀態更新拆開，保留現有可見行為，避免把 WinForms 的 `FilePath`、`Log`、通知框、進度條直接搬入 Core。

重寫邊界：

- Core 層應提供 `.cue`、`.flac`、`.tak` importer，以及 CUE exporter。
- Avalonia/ViewModel 僅負責選檔、調用 importer/exporter、展示錯誤、展示日誌與更新列表。
- 本模塊不覆蓋通用章節編輯、時間平移、XML/TXT/QPF/JSON 等導出格式；這些由核心章節模型與核心導出文檔承接。
- `CueSharp.cs` 是第三方 CueSharp 0.5 風格解析器的本地版本；現有載入路徑實際使用 `CueData.ParseCue`，`CueSharp.CueSheet` 主要被測試覆蓋並提供更完整的 CUE sheet 模型參考。

## 2. 審閱過的文件清單

- `Time_Shift/Util/ChapterData/CueData.cs`
- `Time_Shift/Util/ChapterData/FlacData.cs`
- `Time_Shift/Util/CueSharp.cs`
- `Time_Shift/ChapterData/IData.cs`
- `Time_Shift/Forms/Form1.cs` 中 `LoadCue`、文件類型分派、保存類型與 CUE 導出分支
- `Time_Shift/Util/ChapterInfo.cs` 中 `GetCue`
- `Time_Shift/Util/ToolKits.cs` 中 `GetUTFString`、`ToCueTimeStamp`、`SaveAs`
- `Time_Shift/Util/Chapter.cs`、`Time_Shift/Util/ChapterInfo.cs` 的章節字段
- `Time_Shift_Test/Util/CueDataTests.cs`
- `Time_Shift_Test/Util/CueSheetTests.cs`
- `Time_Shift_Test/[cue_Sample]/*.cue`
- `Time_Shift_Test/Time_Shift_Test.csproj` 中 CUE 測試資產引用與 NuGet 測試依賴
- `Time_Shift.sln`、`README.md`、`ChangeLog.md`、`LICENSE` 中與 CUE/FLAC/TAK 支持、歷史修正與授權相關的上下文

## 3. `.cue`、`.flac`、`.tak` 載入路徑與字段映射

### 3.1 UI 載入路徑

WinForms 中 `SupportTypes` 把 CUE 類過濾器映射到 `cue`、`tak`、`flac`。`LoadFile` 根據副檔名解析成 `FileType.Cue`、`FileType.Tak`、`FileType.Flac` 後統一調用 `LoadCue`。

`LoadCue` 的現有行為：

- `new CueData(FilePath, Log).Chapter` 生成單個 `ChapterInfo`。
- 成功後進度條設為 `33`，提示載入成功。
- 失敗時彈出錯誤、寫入 `ERROR(LoadCue) ...`，並把 `FilePath` 清空。
- `LoadFile` 返回前會對 `_info` 調用 `UpdateInfo(_chapterNameTemplate)`，因此 importer 不負責套用章節名模板或 UI 行資料。

Avalonia 重寫時應將這條路徑拆為：

- `IChapterImporter.CanImport` 判斷副檔名。
- `ImportAsync` 返回 `ChapterImportResult`。
- ViewModel 根據結果更新當前 `ChapterInfo`、提示與日誌。
- 本模塊沒有外部進程或 CLI 依賴；CUE、FLAC、TAK 都是 file/stream based 的進程內解析，和 Matroska/BDMV importer 的工具調用邊界不同。

### 3.2 `.cue` 文件

`.cue` 文件由 `CueData` 直接讀取字節並調用 `GetUTFString`：

- UTF-8 BOM：跳過 BOM 後用 UTF-8。
- UTF-16 LE BOM：用 `Encoding.Unicode`。
- UTF-16 BE BOM：用 `Encoding.BigEndianUnicode`。
- 無 BOM：按 UTF-8 解碼。

空字符串會拋出 `InvalidDataException("Empty cue file")`。非空文本交給 `ParseCue`。

### 3.3 `.flac` 內嵌 CUE

`.flac` 路徑調用 `GetCueFromFlac`，內部使用 `FlacData.GetMetadataFromFlac`：

- 小於 `1 << 20` 字節的 FLAC 直接返回空 `FlacInfo`，最終表現為未找到 CUE。
- 文件頭必須是 ASCII `fLaC`，否則拋出 `InvalidDataException`。
- 解析 FLAC metadata block header：1 bit last flag、7 bit block type、24 bit length。
- 支持讀取 `STREAMINFO`、`VORBIS_COMMENT`、`PICTURE`，其他已知塊含 `CUESHEET` 會跳過。
- 內嵌 CUE 只從 Vorbis comment 的鍵 `cuesheet` 讀取，不使用 FLAC 原生 `CUESHEET` metadata block。
- Vorbis comment 按 UTF-8 解碼，key/value 用第一個 `=` 切分，存入大小寫敏感的 `Dictionary<string,string>`。
- 只有 enum 已知的 `PADDING`、`APPLICATION`、`SEEKTABLE`、`CUESHEET` 等非目標 block 會被跳過；未知或 reserved block type 會透出 `ArgumentOutOfRangeException`。
- malformed Vorbis comment 若不包含 `=`，`ParseVorbisComment` 會因 index/substring 處理失敗而拋一般異常，沒有專用錯誤碼。

注意：現有代碼只檢查小寫鍵 `cuesheet`。常見的大寫 `CUESHEET` 或混合大小寫目前不會命中，重寫時需決定是否保持兼容或新增大小寫不敏感匹配。

### 3.4 `.tak` 內嵌 CUE

`.tak` 路徑調用 `GetCueFromTak`：

- 小於 `1 << 20` 字節直接返回空字符串。
- 文件頭必須是 ASCII `tBaK`，否則拋出 `InvalidDataException`。
- 從文件尾部向前 seek 20480 字節，讀取尾部緩衝區。
- 在緩衝區中查找大小寫不敏感的 `cuesheet` 標記。
- 標記命中後從 `i + 2` 開始取 CUE 文本，直到連續 6 個 `0x00` 作為 TAK 終止條件。
- 文本按 UTF-8 解碼。

同一個底層 `GetCueSheet` 也保留了 FLAC 類型分支，FLAC 終止條件是連續 3 個 `0x00`，但現有 `.flac` 載入路徑沒有使用這個掃描分支。

### 3.5 `CueData.ParseCue` 解析算法

`ParseCue` 是簡化狀態機：

- 初始狀態 `NsStart`：讀取全局 `TITLE` 作為 `ChapterInfo.Title`；讀取第一個 `FILE` 後設置 `ChapterInfo.SourceName` 並進入 track 掃描。
- `NsNewTrack`：忽略非空非 `TRACK` 行；遇空白行直接結束解析；遇 `TRACK <number>` 建立 `Chapter` 並記錄 `Number`。
- `NsTrack`：讀取 track 內 `TITLE` 作為章節名；讀取 `PERFORMER` 時追加到章節名，格式為 `原標題 [演出者]`；讀取 `INDEX`。
- `INDEX 00` 被視為 pre-gap 並忽略。
- `INDEX 01` 轉為章節時間並加入 `cue.Chapters`，然後回到 `NsNewTrack`。
- 其他 `INDEX` 編號會把狀態設為 `NsError`，下一輪進入錯誤分支時拋出 `Unable to Parse this cue file`。
- 結束後若沒有任何章節，拋出 `Empty cue file`。
- 章節按 `Number` 排序，`Duration` 設為最後一個章節的開始時間。

時間換算：

- CUE 時間格式為 `MM:SS:FF`。
- `FF` 是 75 fps 的 CD frame，不是 10 毫秒。
- 現有換算是 `Math.Round(frames * 1000F / 75)` 得到毫秒。
- 用 `new TimeSpan(0, 0, minute, second, millisecond)` 建立時間，因此 CUE 的分鐘值可超過 59，會自然折算為小時。

### 3.6 字段映射

`CueData.ParseCue` 到核心模型：

- `ChapterInfo.SourceType` = `"CUE"`
- `ChapterInfo.Tag` = 原始 CUE 字符串
- `ChapterInfo.TagType` = `typeof(string)`
- 全局 `TITLE` -> `ChapterInfo.Title`
- `FILE "<name>" <type>` -> `ChapterInfo.SourceName`
- `TRACK <number>` -> `Chapter.Number`
- track `TITLE` -> `Chapter.Name`
- track `PERFORMER` -> 追加到 `Chapter.Name`，格式為 ` [performer]`
- `INDEX 01 MM:SS:FF` -> `Chapter.Time`
- `ChapterInfo.Duration` = 最後一個章節開始時間，不是媒體總時長

`CueSharp.CueSheet.ToChapterInfo` 到核心模型：

- `CueSheet.Title` -> `ChapterInfo.Title`
- `SourceType` = `"CUE"`
- `Tag` = `CueSheet` 實例
- `TagType` = `typeof(CueSheet)`
- 每個 `Track` -> `Chapter`
- 章節名固定為 `$"{track.Title} [{track.Performer}]"`，即使 performer 為空也會產生空括號。
- 時間使用 `track.Index01`，其實取 `Indices.Last().Time`，不是嚴格查找編號 01。

## 4. CUE sheet 語法支持範圍、錯誤與容錯

### 4.1 現有主載入器 `CueData` 支持範圍

`CueData` 的正則和狀態機支持：

- 全局 `TITLE "..."`
- 第一個 `FILE "<filename>" WAVE|MP3|AIFF|BINARY|MOTOROLA`
- `TRACK <number>`，不校驗 track data type。
- track `TITLE "..."`
- track `PERFORMER "..."`
- `INDEX <number> MM:SS:FF`
- `INDEX 00` 忽略，`INDEX 01` 作為章節點。

現有主載入器會忽略：

- `REM`
- `CATALOG`
- `ISRC`
- `FLAGS`
- `PREGAP`、`POSTGAP`
- `SONGWRITER`
- `CDTEXTFILE`
- track data type，例如 `AUDIO`
- 多個 `FILE` 的切換語義
- `FILE` 類型中不在正則列舉內的類型

容錯特性：

- `TITLE` 非必需；先遇到 `FILE` 也可開始解析。
- 全局 `FILE` 前的未知行會被忽略。
- track 間未知行通常被忽略。
- 遇到第一個空白行會終止後續解析，因此中間空行會截斷章節。
- 行首未 trim；正則沒有錨點但要求關鍵字本身匹配，因此縮進通常不影響 track 內行，但含前綴垃圾的匹配行也可能被識別。
- `RTitle`、`RPerformer` 使用 `(.+)`，不支持空標題；引號內嵌引號未專門處理。
- `RTime` 要求分鐘和秒為兩位數，不能解析一位數時間。
- `INDEX 02` 等非 0/1 index 會導致解析錯誤。

### 4.2 `CueSharp.CueSheet` 支持範圍

`CueSharp` 支持更完整的 CUE sheet 對象模型：

- 全局：`CATALOG`、`CDTEXTFILE`、`REM`、`PERFORMER`、`SONGWRITER`、`TITLE`
- 文件：`FILE`
- track：`TRACK`、`FLAGS`、`INDEX`、`ISRC`、`PERFORMER`、`SONGWRITER`、`TITLE`、`PREGAP`、`POSTGAP`、track 內 `REM`
- file type enum：`BINARY`、`MOTOROLA`、`AIFF`、`WAVE`、`MP3`
- data type enum：`AUDIO`、`CDG`、`MODE1/2048`、`MODE1/2352`、`MODE2/2336`、`MODE2/2352`、`CDI/2336`、`CDI/2352`
- flags：`DCP`、`4CH`、`PRE`、`SCMS`、`DATA`

`CueSharp` 的容錯模型是把不認識或語法錯誤的行存入 `Garbage`，但部分解析仍可能因 substring、數字轉換、空行處理而拋例外。構造器會刪除空行，因此不像 `CueData` 那樣被中間空行截斷。

重寫建議：Core importer 可使用一個統一 parser，但應明確選擇兼容目標。若以現有 UI 行為為準，主 importer 應匹配 `CueData` 的輸出；若引入 `CueSharp` 的完整語法，需要補測避免章節名、空 performer、Index01 選擇策略產生行為回歸。

## 5. CUE 導出與核心導出文檔的接口關係

WinForms 的保存類型中 `SaveTypeEnum.CUE` 對應副檔名 `.cue`。`SaveFile` 分支調用：

`_info.GetCue(Path.GetFileName(FilePath), AutoGenName).SaveAs(savePath)`

保存前 `SaveFile` 會先調用 `UpdateGridView()`，因此 CUE 內容會使用當前幀率、表達式、自動命名和表格編輯後狀態。保存路徑由共用 `GeneRateSavePath()` 產生：基於來源檔名或 `_bdmvTitle`、`SaveTypeSuffix.CUE` 的 `.cue` 副檔名，並以 `_1`、`_2`... 避免覆蓋既有文件。

`ChapterInfo.GetCue` 現有輸出格式：

```cue
REM Generate By ChapterTool
TITLE "<ChapterInfo.Title>"
FILE "<sourceFileName>" WAVE
  TRACK 01 AUDIO
    TITLE "<chapter name>"
    INDEX 01 MM:SS:FF
```

導出規則：

- `sourceFileName` 來自當前載入文件的檔名，不是 `ChapterInfo.SourceName`。
- `FILE` 類型固定寫為 `WAVE`，即使來源是 `.cue`、`.flac`、`.tak`、`.mp3` 或其他格式。
- track 編號重新從 1 遞增，不使用 `Chapter.Number`。
- 跳過 `TimeSpan.MinValue` 的章節。
- `AutoGenName` 為 true 時用章節名模板生成名稱，否則使用 `Chapter.Name`。
- `ToCueTimeStamp` 把 `TimeSpan` 轉為 `MM:SS:FF`，分鐘為 `Hours * 60 + Minutes`，frame 為 `Round(milliseconds * 75 / 1000F)`，現有上限是 `99`，不是 CD frame 的 `74`。
- `SaveAs(object)` 以 UTF-8 BOM 寫出。

與核心導出文檔的接口關係：

- CUE exporter 應實現通用 `IChapterExporter`，輸入為核心 `ChapterInfo`、輸出目標與 exporter options。
- CUE exporter options 至少需要 `SourceFileName`、`AutoGenerateNames`、`Encoding/BomPolicy`。
- UI 不應直接依賴 `ChapterInfo.GetCue` 或 `Path.GetFileName(FilePath)`；ViewModel 應把當前媒體檔名作為 exporter option 傳入。
- 如果核心導出文檔定義了統一保存命名策略，CUE exporter 只負責內容，不負責 `_1.cue` 這類避重命名。

## 6. 對應測試與樣例文件

現有測試：

- `CueDataTests.ParseCueTest`
  - 讀取 `Time_Shift_Test/[cue_Sample]/ARCHIVES 2.cue`。
  - 驗證章節數為 4。
  - 驗證 `INDEX 01` frame 到毫秒的換算，例如 `15:19:21` -> `00:15:19.280`。
  - 驗證 track performer 追加到章節名，例如 `初色bloomy [初春飾利(豊崎愛生)]`。
- `CueSheetTests.CueSheetTest`
  - 用 `new CueSheet(path)` 解析 `ARCHIVES 2.cue`。
  - 調用 `ToChapterInfo`。
  - 輸出每個 track 的 title、performer、最後一個 index time。
  - 目前沒有 assert，只能算煙霧測試。

樣例文件：

- `Time_Shift_Test/[cue_Sample]/ARCHIVES 2.cue`
  - 覆蓋 `CATALOG`、全局 `PERFORMER`、全局 `TITLE`、`FILE ... WAVE`、track `TITLE`、`PERFORMER`、`ISRC`、`INDEX 00/01`。
- `Time_Shift_Test/[cue_Sample]/example-cue-sheet-1.cue`
  - 覆蓋 `FILE ... MP3`、19 條 track、多數 track 帶 performer、分鐘值超過 60。
- `Time_Shift_Test/[cue_Sample]/のんのんびより りぴーと オリジナルサウンドトラック.cue`
  - 覆蓋 `REM GENRE`、日文文件名與標題、大量 track、每條 track 都有 `INDEX 00` 和 `INDEX 01`、分鐘值超過 60。

建議補充測試：

- `.cue` UTF-8 BOM、UTF-16 LE/BE、無 BOM 的解碼。
- CUE 中間空行截斷行為是否保留。
- `INDEX 02` 的錯誤模型。
- 小寫或大寫 Vorbis comment 鍵 `cuesheet`/`CUESHEET`。
- FLAC 原生 `CUESHEET` block 是否仍忽略。
- TAK 尾部掃描的正負樣例。
- CUE 導出 frame 上限是否應修正為 74；若修正，需記錄行為變更。

## 7. Avalonia/Core 建議 importer 接口與錯誤模型

建議 Core importer 接口：

```csharp
public interface IChapterImporter
{
    string Id { get; }
    IReadOnlySet<string> SupportedExtensions { get; }
    bool CanImport(ChapterImportRequest request);
    ValueTask<ChapterImportResult> ImportAsync(
        ChapterImportRequest request,
        CancellationToken cancellationToken = default);
}
```

建議 request/result：

```csharp
public sealed record ChapterImportRequest(
    string FilePath,
    Stream? Content = null,
    Encoding? PreferredEncoding = null);

public sealed record ChapterImportResult(
    ChapterInfo? ChapterInfo,
    IReadOnlyList<ChapterImportDiagnostic> Diagnostics,
    ChapterImportFailure? Failure = null);

public sealed record ChapterImportDiagnostic(
    ChapterDiagnosticSeverity Severity,
    string Code,
    string Message,
    int? Line = null);
```

建議錯誤分類：

- `UnsupportedExtension`：副檔名不在 `.cue`、`.flac`、`.tak`。
- `EmptyCueFile`：CUE 文本為空，或解析後沒有章節。
- `InvalidContainerHeader`：FLAC/TAK 文件頭不匹配。
- `EmbeddedCueNotFound`：FLAC/TAK 沒有找到可解析的 CUE。
- `MalformedCueSyntax`：CUE 語法無法解析，例如不支持的 index。
- `UnsupportedCueFeature`：識別到但暫不支持的語義，例如多文件 CUE 或 FLAC 原生 CUESHEET block。
- `IoError`：文件讀取錯誤。

設計建議：

- importer 不直接依賴 UI logger；返回 diagnostics，由 ViewModel 決定顯示或記錄。
- 新工程應拆出具體文件/服務：`CueChapterImporter`、`CueSheetParser` 或 `CueSharp` adapter、`FlacEmbeddedCueReader`、`TakEmbeddedCueScanner`、`CueChapterExporter`。
- Avalonia 層以 `ChapterImportViewModel.LoadFileCommand` / `SaveCommand` 或同等命令替代 `Form1.LoadFile`、`LoadCue`、`SaveFile` 分支；命令只編排 importer/exporter，不直接解析容器。
- FLAC metadata reader 可獨立為 `IFlacMetadataReader`，方便用 byte array/stream 測試。
- TAK 內嵌 CUE 掃描應保留為小函數並以固定 byte fixtures 測試。
- `ChapterInfo.Tag` 在新模型中應避免保存任意 object；可改為 `RawSource`、`SourceMetadata` 或專用 record，降低 UI/序列化耦合。
- 若採用 `CueSharp` 作為 parser 基礎，應修正 `Index01` 命名與實際取 `Last()` 的差異，或在文檔/測試中明確保留。

## 8. 未確定或需測試驗證的點

- `.flac` 只讀 Vorbis comment 的小寫 `cuesheet`，是否需要兼容大寫 `CUESHEET`。
- FLAC 原生 `CUESHEET` metadata block 目前被跳過；是否在 Avalonia/Core 中視為不支持還是新增支持。
- `GetCueSheet` 會在查找標記時把 buffer 內大寫 ASCII 原地改為小寫；重寫時應避免副作用，但需驗證是否影響輸出。
- TAK 內嵌 CUE 起點使用 `i + 2`，依賴現有文件布局；需要真實 TAK 樣例驗證。
- 小於 1 MiB 的 FLAC/TAK 直接視為無內嵌 CUE，這是性能優化還是歷史假設需確認。
- `CueData.ParseCue` 遇空白行終止，可能無法解析帶空行的常見 CUE；是否保留兼容需產品決策。
- `CueData.ParseCue` 的 `RFile` 不支持 `MOTOROLA` 之外的新 file type，也不支持未加引號的文件名。
- `CueSharp.ToChapterInfo` 和 `CueData.ParseCue` 對空 performer 的章節名行為不同。
- CUE 導出固定 `FILE ... WAVE` 是否符合用戶期望。
- CUE 導出 frame 上限為 99，與 CUE frame 0-74 的常規約束不一致。
- 導入 `Duration` 使用最後一章開始時間，不是實際媒體長度；依賴此值的 UI 或導出需驗證。
- `IData` indexer 現有邊界判斷是 `index < 0 || index > 1`，`Count` 為 1 時 `index == 1` 也會返回 `Chapter`；重寫接口應修正為標準集合語義。

## 9. 本模塊覆蓋的源文件列表

- `Time_Shift/Util/ChapterData/CueData.cs`
- `Time_Shift/Util/ChapterData/FlacData.cs`
- `Time_Shift/Util/CueSharp.cs`
- `Time_Shift/ChapterData/IData.cs`
- `Time_Shift/Forms/Form1.cs`
- `Time_Shift/Util/ChapterInfo.cs`
- `Time_Shift/Util/ToolKits.cs`
- `Time_Shift/Util/Chapter.cs`
- `Time_Shift_Test/Util/CueDataTests.cs`
- `Time_Shift_Test/Util/CueSheetTests.cs`
- `Time_Shift_Test/[cue_Sample]/ARCHIVES 2.cue`
- `Time_Shift_Test/[cue_Sample]/example-cue-sheet-1.cue`
- `Time_Shift_Test/[cue_Sample]/のんのんびより りぴーと オリジナルサウンドトラック.cue`
- `Time_Shift_Test/Time_Shift_Test.csproj`
- `Time_Shift.sln`
- `README.md`
- `ChangeLog.md`
- `LICENSE`
