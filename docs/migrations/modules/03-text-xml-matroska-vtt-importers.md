# Module 03 - Text/XML/Matroska/WebVTT/OGM 章節解析

## 1. 模塊目的與重寫邊界

本模塊負責把純文本章節、Matroska XML 章節、Matroska 容器章節與 WebVTT cue 轉成 ChapterTool 的章節模型。現有 WinForms 版本的輸出主要是 `ChapterInfo` 或 `XmlGroup`，並由 `Form1` 在載入後套用章節命名、幀率與 UI 選擇狀態。

Avalonia 重寫時建議把本模塊收斂為 Core importer 層：

- 保留格式識別、解析、診斷、外部工具調用與多 Edition 展開。
- 不保留 WinForms UI 狀態更新，例如 `tsProgressBar1`、`tsTips`、`comboBox2`、`Notification`、`MessageBoxButtons`。
- 不讓 Core 直接讀寫 WinForms 控件或全局事件；路徑解析、編碼讀取、`mkvextract.exe` 執行、日誌與錯誤顯示應由可注入服務承擔。
- `StreamUtils.cs` 經審閱與本模塊沒有直接調用關係；它是其他二進制解析器共用的 stream/bit helper，重寫本模塊時不應引入依賴。

## 2. 審閱過的文件清單

- `Time_Shift/Util/ChapterData/OgmData.cs`
- `Time_Shift/Util/ChapterData/VTTData.cs`
- `Time_Shift/Util/ChapterData/XmlData.cs`
- `Time_Shift/Util/ChapterData/MatroskaData.cs`
- `Time_Shift/Util/ChapterData/Serializable/MatroskaChapters.cs`
- `Time_Shift/Util/ChapterData/StreamUtils.cs`
- `Time_Shift/Util/ToolKits.cs`
- `Time_Shift/Forms/Form1.cs`
- `Time_Shift_Test/Util/OgmDataTests.cs`
- `Time_Shift_Test/Util/VTTDataTests.cs`
- `Time_Shift_Test/[ogm_Sample]/00001.txt`
- `Time_Shift_Test/[VTT_Sample]/chapter.vtt`
- `Time_Shift_Test/Time_Shift_Test.csproj`

## 3. 格式行為

### OGM / TXT

識別：

- UI 以副檔名 `.txt` 進入 `LoadOgm()`。
- 內容識別只檢查第一個非首尾空白裁剪後的行是否匹配 `CHAPTER\d+=...`；這一步不保證右側時間合法。
- 時間從該行中用 `ToolKits.RTimeFormat` 抽取，格式為 `hh:mm:ss.sss` 或 `hh:mm:ss,sss`，允許冒號、逗號/點周邊空白；若未匹配，`ToTimeSpan()` 會回傳 `TimeSpan.Zero`。

解析算法：

- `File.ReadAllBytes(FilePath).GetUTFString()` 讀取 UTF-8/UTF-16 BOM 或無 BOM UTF-8 文本。
- `OgmData.GetChapterInfo(text)` 先 `Trim` 首尾空白，再按 `\n` 分行。
- 第一個時間行的時間作為 `initialTime`。
- 狀態機在 `LTimeCode` 與 `LName` 間切換：
  - `CHAPTER\d+=time` 產生候選時間，實際章節時間為 `time - initialTime`。
  - `CHAPTER\d+NAME=name` 產生章節，名稱去掉尾部 `\r`，序號從 1 遞增。
  - 空白行在兩個狀態都跳過。
- 不校驗 `CHAPTER01` 與 `CHAPTER01NAME` 的數字是否一致，也不要求連續編號。

成功輸出：

- 單個 `ChapterInfo`。
- `SourceType = "OGM"`。
- `Tag` 保存原始文本，`TagType` 是 `string`。
- `Duration` 被設為最後一個成功解析章節的時間。
- `Form1.LoadOgm()` 會再調用 `_info.UpdateInfo((int)numericUpDown1.Value)`，只按序號平移重排 `Number`，然後標記載入成功；章節名模板是在後續 `LoadFile()` 中用 `_info.UpdateInfo(_chapterNameTemplate)` 套用。

錯誤/部分解析：

- 第一行不符合 `CHAPTER\d+=...` 時直接拋出 `ERROR: ... <-Unmatched time format`；若 key 形狀正確但右側時間無法匹配，通常會由 `ToTimeSpan()` 靜默轉成 `TimeSpan.Zero`。
- 如果在尚未產生任何章節時遇到格式錯誤，拋出 `Unable to Parse this ogm file`。
- 如果已解析至少一個章節後遇到錯誤，通過 `OgmData.OnLog` 記錄 `+Interrupt: Happened at [...]`，返回已解析部分。
- 若文本最後停在 `LName` 狀態且沒有名稱行，現有代碼不會進入 `LError`，會返回此前章節；因此尾部孤立時間行會被忽略。
- 如果沒有任何章節但流程走到結尾，`info.Chapters.Last()` 會拋出 LINQ empty sequence 相關異常。

重要細節：

- OGM 初始時間歸零是既有行為。第一個章節不是 `00:00:00.000` 時，所有章節都會相對第一個時間點平移。
- `TimeSpan.ToTimeSpan()` 對不匹配時間返回 `TimeSpan.Zero`；OGM 首行只先確認 `CHAPTER\d+=` key 形狀，因此無效時間不一定會報錯。

### WebVTT

識別：

- UI 以副檔名 `.vtt` 進入 `LoadWebVTT()`。
- 內容第一個以空行分隔的區塊必須包含字符串 `WEBVTT`。

解析算法：

- `File.ReadAllBytes(FilePath).GetUTFString()` 讀取文本。
- `VTTData.GetChapterInfo(text)` 先移除所有 `\r`。
- 用空行 `\n\n` 分割為 nodes。
- 跳過 header node，對每個 cue node：
  - 按 `\n` 分行。
  - 丟棄 cue id 等前導行，直到遇到包含 `-->` 的時間行。
  - 要求時間行後至少有一行文本。
  - `Regex.Split(lines[0], "-->")` 後用 `TimeSpan.Parse` 解析開始與結束時間。
  - 只使用 cue start 作為章節時間，`lines[1]` 作為章節名稱。

成功輸出：

- 單個 `ChapterInfo`。
- `SourceType = "WebVTT"`。
- `Tag` 保存原始文本，`TagType` 是 `string`。
- 章節序號從 1 遞增。
- `Form1.LoadWebVTT()` 會調用 `_info.UpdateInfo((int)numericUpDown1.Value)`，只按序號平移重排 `Number`，並標記載入成功；章節名模板同樣由後續 `LoadFile()` 統一套用。

錯誤/部分解析：

- header 不含 `WEBVTT` 時拋出 `ERROR: Empty or invalid file type`。
- 任一 cue node 找不到時間行或時間行後沒有文本時拋出 `+Parser Failed: Happened at [...]`。
- 任一時間無法被 `TimeSpan.Parse` 解析時會透出 .NET 格式異常。
- 沒有部分解析返回模型；已加入的章節不會作為成功結果返回。

重要細節：

- cue end time 被解析但未保存。
- cue id 可存在，因為解析會跳過 `-->` 前的行。
- 只取時間行後第一行文本；多行 cue 文本會被截斷。
- 不支持 WebVTT setting 的可靠解析，例如 `00:00:00.000 --> 00:00:26.000 align:start` 會把右側整段交給 `TimeSpan.Parse`，現有行為可能失敗。

### XML / Matroska Chapter XML

識別：

- UI 以副檔名 `.xml` 進入 `LoadXml()`。
- `XmlDocument.Load(FilePath)` 成功後交給 `GetChapterInfoFromXml(doc)`。
- `XmlData.ParseXml(doc)` 要求根節點名稱正好是 `Chapters`。
- 根節點直接子節點除 XML comment 外必須是 `EditionEntry`。

解析算法：

- `ParseXml` 對每個 `EditionEntry` 產生一個 `ChapterInfo`。
- 每個 Edition 只讀直接子節點中的 `ChapterAtom`，其他 Edition 子節點如 flag/UID 被忽略。
- `ParseChapterAtom(chapterAtom, index)` 遞歸處理：
  - `ChapterTimeStart` 生成 start chapter；缺失或無法解析時仍會生成 start chapter，時間默認為 `00:00:00.000`。
  - `ChapterTimeEnd` 生成 end chapter，但只在 end time 大於 start time 時輸出。
  - `ChapterDisplay/ChapterString` 作為名稱；取不到時名稱為空字符串。
  - 巢狀 `ChapterAtom` 會用相同 index 遞歸展開，輸出順序是 parent start、所有 nested chapters、parent end。
- 每個頂層 `ChapterAtom` 的 index 從 1 遞增；巢狀 atom 繼承父 index。
- 每個 Edition 解析完後，會移除相鄰且時間完全相同的前一個章節，用於去掉 parent start 與第一個 nested start 等冗餘節點。

成功輸出：

- `XmlData.ParseXml` 返回 `IEnumerable<ChapterInfo>`；每個 Edition 一個 `ChapterInfo`。
- `SourceType = "XML"`。
- `Tag` 保存 `XmlDocument`，`TagType` 是 `XmlDocument` 類型。
- `Form1.GetChapterInfoFromXml` 把結果放入 `_infoGroup`，在 UI 中列出 `Edition 01`、`Edition 02` 等，並默認 `_info = _infoGroup.First()`；隨後將 `comboBox2.SelectedIndex` 設為 `ClipSelectIndex`。

錯誤/部分解析：

- 空 XML document element 拋出 `ArgumentException("Empty Xml file")`。
- 根節點不是 `Chapters` 拋出 `Invalid Xml file.\nroot node Name: ...`。
- 根下非 comment 且非 `EditionEntry` 的節點會拋出 `Invalid Xml file.\nEntry Name: ...`。
- 缺失或不匹配時間格式時，`ToTimeSpan()` 可能返回 `TimeSpan.Zero`，不一定拋錯。
- 缺失 `ChapterDisplay` 或 `ChapterString` 時名稱為空字符串。
- `GetChapterInfoFromXml` 對空 Edition 結果沒有明確保護；若 `ParseXml` 沒有產生任何項，`First()` 會拋錯。

重要細節：

- 多 Edition 是一等輸出，應保留為多個候選 chapter set，而不是在 Core 直接合併。
- 巢狀 `ChapterAtom` 被扁平化為線性章節列表。
- `ChapterTimeEnd` 會生成額外章節點，這對片段結尾章節有影響。
- `Serializable/MatroskaChapters.cs` 定義了 `Chapters`、`EditionEntry`、`ChapterAtom`、`ChapterDisplay`、`ChapterProcess` 等 XML serializer 模型；`XmlData.Deserializer` 與 `ToChapterInfo` 可從該模型轉回章節，但 UI 路徑目前使用的是 `XmlDocument` + `ParseXml`，不是 serializer 路徑。

### Matroska / MKV / MKA

識別：

- UI 以副檔名 `.mkv` 或 `.mka` 進入 `LoadMatroska()`。
- 不直接解析 EBML；依賴外部 `mkvextract.exe` 導出 Matroska chapter XML。

`mkvextract.exe` 使用：

- `MatroskaData` 建構時尋找 MKVToolNix 路徑：
- 先讀 `RegistryStorage.Load(@"Software\ChapterTool", "mkvToolnixPath")`；這個 API 名稱像 registry，但實際讀取 `chaptertool.json`。
  - 若沒有保存路徑，嘗試從 Windows registry 找 MKVToolNix 安裝位置：
    - `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKVToolNix` 的 `DisplayIcon`。
    - `HKLM\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MKVToolNix` 的 `DisplayIcon`。
    - `HKCU\Software\mkvmergeGUI\GUI` 的 `mkvmerge_executable`。
  - 找到後通過 `RegistryStorage.Save` 保存到 `chaptertool.json` 的 `Software\ChapterTool/mkvToolnixPath` 項。
  - 若仍未找到，回退到當前執行程序集目錄。
  - 最終要求該目錄下存在 `mkvextract.exe`。
- 找不到 `mkvextract.exe` 時記錄 `Mkvextract Path: ...` 並拋出 `无可用 MkvExtract, 安装个呗~`。
- `GetXml(path)` 執行參數為 `chapters "{path}"`。
- `RunMkvextract` 使用 `Process`：
  - `UseShellExecute = false`
  - `CreateNoWindow = true`
  - `RedirectStandardOutput = true`
  - `StandardOutputEncoding = UTF8`
  - 只讀 stdout，不讀 stderr，也不檢查 exit code。
- stdout 空字符串時拋出 `No Chapter Found`。
- stdout 非空時用 `XmlDocument.LoadXml(xmlresult)` 載入，然後沿用 XML 解析流程。

成功輸出：

- `MatroskaData.GetXml` 成功返回 Matroska chapter XML 的 `XmlDocument`。
- `Form1.LoadMatroska()` 隨後調用 `GetChapterInfoFromXml`，因此成功輸出與 XML 相同：多 Edition 對應 `_infoGroup`，默認顯示第一個 Edition。

錯誤/部分解析：

- `No Chapter Found` 被 UI 作為 info 顯示，並清空 `FilePath`。
- 其他異常顯示為 `Exception caught in function LoadMatroska`，寫入 log，並清空 `FilePath`。
- `mkvextract.exe` stderr、exit code、超時、取消、路徑包含特殊字符等情況沒有結構化處理。
- `mkvextract` 輸出若不是合法 XML，`LoadXml` 異常會透出到 UI catch。

## 4. 測試與樣例

現有測試：

- `Time_Shift_Test/Util/OgmDataTests.cs`
  - 讀取 `Time_Shift_Test/[ogm_Sample]/00001.txt`。
  - 斷言章節名稱與時間。
  - 期望時間從 `00:00:00.000`、`00:00:41.041` 到 `00:08:59.247`。
- `Time_Shift_Test/Util/VTTDataTests.cs`
  - 讀取 `Time_Shift_Test/[VTT_Sample]/chapter.vtt`。
  - 只輸出結果，沒有斷言。

樣例：

- `[ogm_Sample]/00001.txt`
  - 包含首尾空白、空行、`CHAPTER01 = ...` 形式的空白變體。
  - `CHAPTER04` 的時間與名稱之間有空白行。
  - 目前文件尾部存在 `CHAPTER07=00:08:59.247`，但審閱到的工作樹內容沒有 `CHAPTER07NAME=...`。
- `[VTT_Sample]/chapter.vtt`
  - 標準 `WEBVTT` header。
  - 每個 cue 有 cue id、時間行與單行名稱。
  - 部分時間行右側有尾隨空白，現有 `TimeSpan.Parse` 在該樣例上應可接受。

缺口：

- 未找到 `XmlData` 的直接單元測試。
- 未找到 `MatroskaData` 的直接單元測試。
- 未找到 Matroska/XML chapter 樣例文件。
- `StreamUtils` 與本模塊無直接測試關聯。

## 5. Avalonia/Core 建議接口與錯誤模型

建議 Core 層提供不依賴 UI 的 importer 合約：

```csharp
public interface IChapterImporter
{
    string Id { get; }
    IReadOnlyCollection<string> SupportedExtensions { get; }
    ValueTask<ImportProbe> ProbeAsync(ImportSource source, CancellationToken cancellationToken);
    ValueTask<ChapterImportResult> ImportAsync(ImportSource source, ChapterImportOptions options, CancellationToken cancellationToken);
}
```

建議模型：

- `ChapterImportResult`
  - `IReadOnlyList<ChapterSet> Editions`
  - `int DefaultEditionIndex`
  - `bool IsPartial`
  - `IReadOnlyList<ImportDiagnostic> Diagnostics`
  - `object? SourceTag` 或可序列化 metadata，避免直接暴露 WinForms 時代的 `TagType`。
- `ChapterSet`
  - `string SourceType`
  - `string? DisplayName`
  - `IReadOnlyList<ChapterEntry> Chapters`
  - `TimeSpan? Duration`
- `ImportDiagnostic`
  - `Severity`: `Info` / `Warning` / `Error`
  - `Code`: 例如 `InvalidHeader`、`InvalidRootElement`、`ExternalToolMissing`、`NoChaptersFound`、`PartialParse`、`UnsupportedCueSettings`。
  - `Message`
  - `Location`: line/column、cue index、XML node path 或 external process。
  - `Exception?` 僅供 log/debug，不作為 UI 文案唯一來源。

接口行為建議：

- OGM importer 保留「已解析章節後可部分成功」的兼容行為，但用 `IsPartial=true` 與 warning diagnostic 表達。
- VTT importer 可先兼容現有嚴格行為；若要支持 cue settings 和多行 cue text，應新增測試後再擴展。
- XML importer 應回傳多 Edition，不在 Core 層綁定 combo box；UI 層只做選擇。
- Matroska importer 應拆成 `IMkvExtractLocator`、`IExternalProcessRunner`、`IXmlChapterImporter`：
  - 便於單元測試。
  - 支持 cancellation、timeout、stderr、exit code。
  - 可在非 Windows 平台查找 `mkvextract` 而不是硬編碼 `mkvextract.exe` 和 registry。
- 編碼讀取建議集中到 `ITextEncodingDetector` 或 shared helper，至少保留 UTF-8/UTF-16 BOM 兼容。

## 6. 未確定或需測試驗證的點

- OGM 樣例當前缺失 `CHAPTER07NAME`，但 `OgmDataTests` 期望第 7 章名稱為 `Chapter 07`；需要確認樣例是否在其他分支已修正，或測試是否目前會失敗。
- OGM 尾部孤立時間行的既有行為是忽略候選時間並返回前面章節；是否應在 Avalonia 重寫中保留為兼容行為需產品確認。
- `XmlData.ParseXml` 對缺失時間、缺失 `ChapterDisplay`、空 Edition、巢狀 ChapterAtom index 繼承、相鄰同時間去重等行為缺少測試。
- `XmlData.ToChapterInfo(Chapters)` serializer 路徑與 `ParseXml(XmlDocument)` 行為不完全一致，例如沒有相鄰同時間去重，且對 `ChapterDisplay` null 可能拋錯；需決定重寫時是否保留兩條路徑。
- WebVTT 是否需要支持 cue settings、多行 cue payload、NOTE/STYLE/REGION block、逗號小數、無小時格式等完整 WebVTT 語法，需要新增樣例驗證。
- Matroska `mkvextract` 目前不檢查 exit code/stderr；需測試空 stdout、非零退出碼、stderr 有錯、stdout 非 XML 等情況。
- Matroska 工具定位目前是 `chaptertool.json` 保存路徑 + Windows registry 探測 + exe 目錄；Avalonia 跨平台版本需要定義 Linux/macOS 搜索規則與用戶配置入口。
- `GetChapterInfoFromXml` 先設 `_info = _infoGroup.First()` 再設 `comboBox2.SelectedIndex = ClipSelectIndex`；需確認 selection changed 事件是否會覆蓋 `_info`，以及 `ClipSelectIndex` 越界時現有 UI 行為。

## 7. 本模塊覆蓋的源文件列表

Core parser/importer：

- `Time_Shift/Util/ChapterData/OgmData.cs`
- `Time_Shift/Util/ChapterData/VTTData.cs`
- `Time_Shift/Util/ChapterData/XmlData.cs`
- `Time_Shift/Util/ChapterData/MatroskaData.cs`
- `Time_Shift/Util/ChapterData/Serializable/MatroskaChapters.cs`
- `Time_Shift/Util/ToolKits.cs`

審閱但不屬於本模塊重寫主體：

- `Time_Shift/Util/ChapterData/StreamUtils.cs`

WinForms integration：

- `Time_Shift/Forms/Form1.cs`
  - `LoadOgm`
  - `LoadXml`
  - `LoadMatroska`
  - `LoadWebVTT`
  - `GetChapterInfoFromXml`

測試與樣例：

- `Time_Shift_Test/Util/OgmDataTests.cs`
- `Time_Shift_Test/Util/VTTDataTests.cs`
- `Time_Shift_Test/[ogm_Sample]/00001.txt`
- `Time_Shift_Test/[VTT_Sample]/chapter.vtt`
- `Time_Shift_Test/Time_Shift_Test.csproj`
