# Module 04 - Disc/Playlist/Media Container 章節解析

## 1. 模塊目的與重寫邊界

本模塊負責把光盤播放列表、DVD/HD-DVD 結構與 MP4 容器中的章節信息轉成 ChapterTool 的核心章節模型。現有 WinForms 版把二進制/XML/外部工具解析、片段選擇、合併、追加、打開對應視頻文件、錯誤提示混在 `Form1.cs` 事件處理中；Avalonia 重寫時應把解析能力下沉到 Core/Infrastructure，讓 UI 只處理命令、選擇狀態和錯誤呈現。

本模塊覆蓋：

- Blu-ray `.mpls` 播放列表解析。
- DVD `.ifo` PGC/program/cell 章節解析與 `SharpDvdInfo` DVD metadata 讀取。
- HD-DVD `.xpl` playlist XML 解析。
- BDMV 目錄通過 `eac3to` 掃描播放列表並導出章節。
- MP4/M4A/M4V 通過 Knuckleball + `libmp4v2.dll` 讀取章節。
- `Form1.cs` 中與上述格式相關的載入、追加、合併、片段選擇、打開對應視頻文件流程。

重寫邊界：

- Core 保留格式解析、時間換算、多片段展開、合併語義和可測試錯誤結果。
- Infrastructure 負責文件系統、外部進程、Registry/設定、硬鏈接、native DLL、平台 shell open。
- Avalonia ViewModel 負責 `InfoGroup`、`CurrentInfo`、`SelectedClipIndex`、`CombineChapter`、狀態文字、進度和命令可用性。
- Core 不應引用 WinForms 控件、`Notification`、`MessageBox`、`Process.Start`、`RegistryStorage` 或全局靜態 UI log 事件。

## 2. 審閱過的文件清單

核心解析：

- `Time_Shift/Util/ChapterData/MplsData.cs`
- `Time_Shift/Util/ChapterData/IfoData.cs`
- `Time_Shift/Util/ChapterData/IfoParser.cs`
- `Time_Shift/Util/ChapterData/XplData.cs`
- `Time_Shift/Util/ChapterData/BDMVData.cs`
- `Time_Shift/Util/ChapterData/Mp4Data.cs`
- `Time_Shift/Util/ChapterData/StreamUtils.cs`
- `Time_Shift/Util/ChapterInfo.cs`
- `Time_Shift/Util/ChapterInfoGroup.cs`
- `Time_Shift/Util/Chapter.cs`

MP4 native wrapper：

- `Time_Shift/Knuckleball/MP4File.cs`
- `Time_Shift/Knuckleball/Chapter.cs`
- `Time_Shift/Knuckleball/NativeMethods.cs`
- `Time_Shift/Knuckleball/IntPtrExtensions.cs`

DVD metadata：

- `Time_Shift/SharpDvdInfo/DvdInfoContainer.cs`
- `Time_Shift/SharpDvdInfo/DvdTypes/DvdAudio.cs`
- `Time_Shift/SharpDvdInfo/DvdTypes/DvdSubpicture.cs`
- `Time_Shift/SharpDvdInfo/DvdTypes/DvdVideo.cs`
- `Time_Shift/SharpDvdInfo/Model/AudioProperties.cs`
- `Time_Shift/SharpDvdInfo/Model/SubpictureProperties.cs`
- `Time_Shift/SharpDvdInfo/Model/TitleInfo.cs`
- `Time_Shift/SharpDvdInfo/Model/VideoProperties.cs`
- `Time_Shift/SharpDvdInfo/Model/VmgmInfo.cs`

UI 流程：

- `Time_Shift/Forms/Form1.cs` 中 `LoadMpls`、`LoadIfo`、`LoadXpl`、`LoadBDMVAsync`、`LoadMp4`、`appendToolStripMenuItem_Click`、`comboBox2_SelectionChangeCommitted`、`combineToolStripMenuItem_Click`、`GetChapterInfoFromMpls`、`GetChapterInfoFromIFO`、`InsertMpls`、`InsertIfo`、`InsertXpl`、`OpenFile`。
- `Time_Shift/Util/NativeMethods.cs` 中 MP4 硬鏈接相關方法。
- `Time_Shift/Util/TaskAsync.cs` 中 BDMV/eac3to 進程執行 helper。

測試與樣例：

- `Time_Shift_Test/Util/MplsDataTests.cs`
- `Time_Shift_Test/Util/IfoDataTests.cs`
- `Time_Shift_Test/Util/IfoParserTests.cs`
- `Time_Shift_Test/Util/IfoTimeTests.cs`
- `Time_Shift_Test/Knuckleball/MP4FileTests.cs`
- `Time_Shift_Test/SharpDvdInfo/DvdInfoContainerTests.cs`
- `Time_Shift_Test/[mpls_Sample]/*.mpls`
- `Time_Shift_Test/[ifo_Sample]/VTS_05_0.IFO`
- `Time_Shift_Test/[Video_Sample]/Chapter.mp4`

## 3. 格式解析行為

### MPLS / Blu-ray playlist

入口：

- UI 副檔名 `.mpls` 進入 `Form1.LoadMpls()`。
- Core 入口是 `new MplsData(path).GetChapters()`，返回 `MplsGroup`。
- `LoadMpls(setGlobal, addToComboBox, customPath)` 同時負責 UI 狀態、combo box 項目和 append 場景。

解析算法：

- `MplsHeader` 從文件開頭讀取 `TypeIndicator`、`PlayListStartAddress`、`PlayListMarkStartAddress`、`ExtensionDataStartAddress`。
- 文件頭必須是 `MPLS`，版本只接受 `0100`、`0200`、`0300`。
- 按 header offset 讀取 `PlayList`、`PlayListMark`，若 extension offset 非 0 則讀取 `ExtensionData`。
- `PlayList` 展開 `PlayItems` 與 `SubPaths`。每個 `PlayItem` 包含 clip name、IN/OUT PTS、multi-angle、STN stream table。
- `STNTable` 讀 primary video/audio/PG/IG、secondary audio/video/PG stream entries。`StreamAttribution` 只記錄 stream coding、語言、聲道、採樣率、分辨率、幀率，不改變輸出模型。
- `GetChapters()` 對每個 `PlayItem` 生成一個 `ChapterInfo`。
- 每段取第一個 `PrimaryVideoStreamEntry` 的 `StreamAttributes.FrameRate`，映射到 `MplsData.FrameRate`：
  - `0`: 0
  - `1`: 24000/1001
  - `2`: 24
  - `3`: 25
  - `4`: 30000/1001
  - `5`: 0
  - `6`: 50
  - `7`: 60000/1001
- 章節 mark 篩選條件是 `MarkType == 0x01 && RefToPlayItemID == playItemIndex`。
- 時間戳以第一個該 play item 的 mark 為 offset；若 `INTime < firstMarkTimeStamp`，offset 改用 `INTime` 並記錄 log。
- PTS 換算：`Pts2Time(pts)` 使用 `pts / 45000` 得到秒，毫秒用 `MidpointRounding.AwayFromZero` 四捨五入。
- 沒有任何章節 mark 的 play item 仍產生一個 `00:00:00`、`Chapter 1` 的章節，並記錄 log。
- multi-angle `FullName` 會把主 clip 和 angle clip 用 `&` 連接，例如 `00006&00007`。

成功輸出字段：

- `SourceType = "MPLS"`
- `SourceName = PlayItem.FullName`，不含 `.m2ts`。
- `Duration = Pts2Time(playItem.OUTTime - playItem.INTime)`。
- `FramesPerSecond = FrameRate[primaryVideo.FrameRate]`。
- `Chapters`：每個 chapter 有 `Time`、遞增 `Number`、`ChapterName` 生成的 `Name`。
- `Title`、`SourceIndex` 不設置。

錯誤行為：

- 無效文件頭或版本直接拋 `Exception`。
- 沒有 primary video stream 時，`First(item => item is PrimaryVideoStreamEntry)` 會拋 LINQ 異常。
- 未知 stream type/coding type 只 `Console.WriteLine`，不阻止解析。
- `LoadFile()` 會捕捉異常、顯示錯誤、清空 `FilePath`、重置進度和路徑文字。
- `LoadMpls()` 本身只有 `finally` 解除 log 訂閱，沒有局部 catch。

二進制 helper：

- `StreamUtils` 提供未檢查短讀的 `ReadBytes`、BE/LE 16/24/32/64-bit 整數讀取，以及 `Skip`。
- `Skip` 會先 seek，若 seek 後位置超過 stream 長度才拋 `Skip out of range`。
- 短讀不會立即轉成格式化解析錯誤；截斷或畸形二進制輸入可能在後續位移、索引或轉換時才以一般異常暴露。
- `BitReader` 複製來源 buffer 並按 MSB-first 讀 bit；讀越界時會透出 `IndexOutOfRangeException`，沒有專用 parse diagnostic。

### IFO / DVD

入口：

- UI 副檔名 `.ifo` 進入 `Form1.LoadIfo()`。
- Core 入口是 `IfoData.GetStreams(ifoFile)`，返回多個 `ChapterInfo`，UI 包成 `IfoGroup`。
- `SharpDvdInfo.DvdInfoContainer` 是另一條 DVD information 讀取路徑，測試中覆蓋，但 `Form1.LoadIfo()` 使用的是 `IfoData`。

`IfoData` 章節算法：

- `IfoParser.GetPGCnb(ifoFile)` 讀 PGC 數量，`GetStreams()` 對 `1..pgcCount` 逐個產生 `ChapterInfo`。
- `GetChapterInfo(location, titleSetNum)` 若文件名匹配 `VTS_(\d+)_0.IFO`，用文件名中的 VTS number 覆蓋 `titleSetNum`。這使普通 `VTS_05_0.IFO` 通常只輸出同一個 title set，而不是真正遍歷所有 PGC。
- `SourceType = "DVD"`。
- 若無副檔名文件名包含兩個 `_`，設置 `Title` 和 `SourceName` 為 `VTS_05_{titleSetNum}` 形式。
- `GetChapters()`：
  - 打開 IFO 文件。
  - `GetPCGIP_Position()` 從 `0xCC` 讀 sector offset 並乘 `0x800`。
  - 取得 program chain offset 和 program 數量。
  - 首章固定為 `Chapter 01` at `00:00:00`。
  - 從 PGC program map 取得每個 program 的 entry cell/exit cell。
  - 從 cell playback table 對 cell type `0x00` 或 `0x01` 的 cell 讀取 4 byte DVD playback time。
  - playback time 是 BCD hour/min/sec/frame，幀率 mask `0x01` 表示 PAL 25fps，`0x03` 表示 NTSC raw 30fps/實際 `30000/1001`。
  - 用 `IfoTimeSpan` 累計 program duration；每個非最後 program 的累計時間生成下一章。
- `IfoTimeSpan` 內部保存 `TotalFrames` 與 `IsNTSC`，轉 `TimeSpan` 時 NTSC 使用 `30000/1001`。
- 總時長小於 10 秒的 `ChapterInfo` 被置為 `null`，UI 會 `.Where(item => item != null)` 過濾。

成功輸出字段：

- `SourceType = "DVD"`。
- `Title` / `SourceName = VTS_xx_y`，取決於文件名規則。
- `Duration` 是所有 program cell duration 累計。
- `FramesPerSecond = 30000/1001` 或 `25`。
- `Chapters`：`Chapter 01`、`Chapter 02`...，時間為累計 program 起點。
- UI 載入後會重排序號為 1..N。

錯誤行為：

- IFO 位置為負時 `GetFileBlock()` 拋 `Exception("Invalid Ifo file")`。
- BCD/time 讀取失敗時 `IfoParser.ReadTimeSpan()` 記錄 `Logger.Log(exception.Message)` 並返回 `null`。
- `ReadTimeSpan()` 只 `Debug.Assert` 幀率 mask 為 25/30，Release 下不會阻止其他 mask。
- `IfoTimeSpan` 在 NTSC/PAL 混加、比較時拋 `InvalidOperationException("Unmatch frames rate mode")`。
- 沒有可用 chapter 時 `LoadIfo()` 拋 `No Chapter detected in ifo file`。

`SharpDvdInfo` 行為：

- `new DvdInfoContainer(path)` 可接受單個 `VTS_nn_0.IFO` 文件或 DVD 目錄。
- 文件模式下要求文件名匹配 `VTS_(\d{2})_0.IFO`，否則拋 `Invalid file`。
- 目錄模式會定位 `VIDEO_TS`，讀 `VIDEO_TS.IFO` 的 VMGM title table，再讀每個 `VTS_nn_0.IFO`。
- 讀取 video standard、resolution、aspect ratio、runtime、audio streams、subtitle streams、chapters。
- `GetChapterInfo()` 輸出 `List<ChapterInfo>`，每個 title 的 `SourceName = VTS_{TitleSetNumber:00}_1`、`SourceType = "DVD"`，章節名稱用 `ChapterName` 自動生成。
- 目前 `SharpDvdInfo` 輸出的 `ChapterInfo` 沒有設置 `Duration` 和 `FramesPerSecond`，章節列表包含初始 0 和每個 cell 累計點；測試中比 `IfoData` 多一個尾部時間點。

### XPL / HD-DVD playlist

入口：

- UI 副檔名 `.xpl` 進入 `Form1.LoadXpl()`。
- Core 入口是 `XplData.GetStreams(location)`，UI 包成 `XplGroup`。

解析算法：

- 用 `XDocument.Load(location)` 讀 XML。
- 固定命名空間：`http://www.dvdforum.org/2005/HDDVDVideo/Playlist`。
- 遍歷 `Playlist/TitleSet`。
- `timeBase` 從 TitleSet attribute 解析，缺失時默認 `60`。
- `tickBase` 從 TitleSet attribute 解析，缺失時默認 `24`。
- 只處理包含 `ChapterList` 的 `Title`。
- `tickBaseDivisor` 從 Title attribute 解析，缺失時默認 `1`。
- `SourceName` 取 `PrimaryAudioVideoClip/@src`，缺失時空字符串。
- `Duration` 由 `titleDuration` 解析。
- `Title` 默認為 XPL 文件名；若 `Title/@id` 存在則覆蓋，若 `Title/@displayName` 存在再覆蓋。
- 每個 `Chapter` 的名稱默認空字符串；`id` 可覆蓋，`displayName` 再覆蓋。
- `titleTimeBegin` 解析為章節時間。
- 時間字符串格式是 `HH:MM:SS:TT`。最後一個冒號前交給 `TimeSpan.Parse`；主時間再按 `timeBase / 60` 換算，尾部 tick 按 `tickBase / tickBaseDivisor` 換算。

成功輸出字段：

- `SourceType = "HD-DVD"`。
- `SourceName = PrimaryAudioVideoClip/@src` 或空字符串。
- `FramesPerSecond = 24M`，不隨 `timeBase`/`tickBase` 改變。
- `Title` 取 displayName/id/文件名。
- `Duration` 取 `titleDuration`。
- `Chapters`：`Name` 取 displayName/id/空字符串，`Time` 取 `titleTimeBegin`。
- UI 載入後會重排序號為 1..N，combo item 為 `{Title}__{ChapterCount}`。

錯誤行為：

- XML 結構或命名空間不匹配時，`doc.Element(ns + "Playlist")` 等 null dereference 會拋異常。
- `timeBase`/`tickBase` 解析用 `float.Parse`，非法字符串拋異常。
- 缺少 `titleDuration` 或 `titleTimeBegin` 會在 `GetTimeSpan()` 中因 null/substring 失敗而拋異常。
- `LoadXpl()` 若沒有任何 title/chapter，拋 `No Chapter detected in xpl file`。

### BDMV / Blu-ray directory

入口：

- UI 拖放目錄時設 `_isUrl = true` 並調用 `LoadBDMVAsync()`。
- Core-like 入口是 `BDMVData.GetChapterAsync(location)`，返回 `KeyValuePair<string, BDMVGroup>`，key 是 disc title。
- 現有代碼沒有通過文件選擇器載入 BDMV 目錄，只在拖放目錄和 reload BDMV 狀態下進入。

解析算法：

- 驗證 `location/BDMV/PLAYLIST` 存在，否則拋 `FileNotFoundException("Blu-Ray disc structure not found.")`。
- 若存在 `BDMV/META/DL/*.xml`，讀第一個 XML 文件，用 regex `<di:name>` 取 disc title 並記錄 log。
- 從 `RegistryStorage.Load<string>("eac3toPath")` 讀 `eac3toPath`；該 API 實際讀取可執行文件目錄下的 `chaptertool.json`，不是 Windows registry。不存在或文件無效時，用 WinForms `Notification.InputBox` 要求輸入；取消則返回空 `BDMVGroup`。
- 通過 `RegistryStorage.Save(eac3toPath, "eac3toPath")` 保存新的路徑到 `chaptertool.json`。
- `workingPath = Directory.GetParent(location).FullName`，再把 `location` 改成最後一段目錄名；後續 eac3to 在父目錄工作，以相對目錄名作參數。
- 第一次執行：`eac3toPath "{discDirName}"`，解析 disc playlist 摘要。
- 若輸出包含 `HD DVD / Blu-Ray disc structure not found.`，記錄完整輸出並拋 `May be the path is too complex or directory contains nonAscii characters`。
- 用 regex 匹配形如 `idx) playlist.mpls, duration file.m2ts` 或 `idx) playlist.mpls, file.m2ts, duration` 的行。
- 對每個匹配建立 `ChapterInfo`，設置 `Duration`、`SourceIndex`、`SourceName`。此階段不設 `SourceType`。
- 對每個候選 playlist：
  - 執行 `eac3to "{discDirName}" {SourceIndex})`，若輸出不含 `Chapters` 則標記移除。
  - 執行 `eac3to "{discDirName}" {SourceIndex}) chapters.txt` 導出章節。
  - 若輸出不含 `Creating file "chapters.txt"...` 且不含 `Done!`，記錄輸出並拋 `Error creating chapters file.`。
  - 讀 `workingPath/chapters.txt`，用 `OgmData.GetChapterInfo(...).Chapters` 轉章節。
  - 若第一個章節名為空字符串，為所有章節生成 `Chapter NN` 名稱。
- 移除沒有 chapter 的候選項，刪除 `chapters.txt` 和 `chapters - Log.txt`。

成功輸出字段：

- 返回值 key：BDMV metadata 中的 disc title，可能為空字符串。
- `BDMVGroup` 中每個 `ChapterInfo`：
  - `SourceIndex = eac3to` 列表索引字符串。
  - `SourceName = eac3to` 輸出的 `.m2ts` 文件或片段列表字符串。
  - `Duration = eac3to` 輸出的時長。
  - `Chapters = chapters.txt` 經 OGM parser 解析出的章節。
  - `SourceType`、`FramesPerSecond`、`Title` 不設置。
- `LoadBDMVAsync()` 把 `_bdmvTitle` 設為返回 key，`_infoGroup` 設為返回 group，`_info` 設為第一項，combo item 為 `{SourceName}__{ChapterCount}`。

錯誤行為：

- BDMV 結構不存在直接拋 `FileNotFoundException`。
- 缺少 eac3to 且用戶取消輸入時返回空 group；UI 顯示載入失敗但不彈異常。
- eac3to 無法識別結構時拋一般 `Exception`，訊息指向路徑過複雜或含非 ASCII。
- 章節文件導出失敗拋 `Exception("Error creating chapters file.")`。
- 外部輸出 regex 不匹配時會得到空 group。
- `LoadBDMVAsync()` catch 後顯示 `Exception thrown while loading BluRay disc`，不清空 `FilePath`。

### MP4 / M4A / M4V

入口：

- UI 副檔名 `.mp4`、`.m4a`、`.m4v` 進入 `Form1.LoadMp4()`。
- Core 入口是 `new Mp4Data(path).Chapter`。
- Native wrapper 入口是 `Knuckleball.MP4File.Open(path)`。

解析算法：

- UI 先檢查當前工作目錄是否存在 `libmp4v2.dll`。缺失時清空 `FilePath`，詢問是否打開下載頁，然後返回。
- 建立臨時硬鏈接路徑：`Path.GetPathRoot(FilePath) + Guid.NewGuid()`。
- 調用 `NativeMethods.CreateHardLinkCMD(linkedFile, FilePath)`，實際執行 `fsutil hardlink create`。
- 若硬鏈接存在，用硬鏈接路徑讀 MP4；否則回退原路徑。最後刪除硬鏈接。
- `MP4File.Open()` 驗證文件存在，`Load()` 調用 `NativeMethods.MP4Read()`。
- `MP4Read()` 返回 `IntPtr.Zero` 時直接返回，`Chapters` 保持 null。
- `ReadFromFile()` 調用 `MP4GetChapters(..., MP4ChapterType.Any)`。
- 若 chapter type 不是 `None` 且 chapter count 非 0：
  - 按 native `MP4Chapter` 結構逐條 marshal。
  - duration 按毫秒轉 `TimeSpan`。
  - title 支持 UTF-8、UTF-16LE BOM、UTF-16BE BOM、UTF-8 BOM，最後截斷到第一個 `\0`。
- 若沒有 native chapters：
  - 讀 `MP4GetTimeScale()` 和 `MP4GetDuration()`。
  - 產生一個 `Duration = duration / timeScale`、`Title = "Chapter 1"` 的 fallback chapter。
- `Mp4Data` 把 Knuckleball 的「每章 duration」轉成 ChapterTool 的「每章 start time」：
  - 初始 `ChapterInfo.Duration = 0`。
  - 每個 native chapter 以當前累計 duration 作為 `Chapter.Time`。
  - 再把 native chapter duration 加到 `ChapterInfo.Duration`。

成功輸出字段：

- `Mp4Data.Chapter` 是單個 `ChapterInfo`。
- `Chapters`：`Name = native title`，`Time = 累計開始時間`，`Number = 1..N`。
- `Duration = 所有 native chapter duration 累計`。
- `SourceType`、`SourceName`、`FramesPerSecond`、`Title` 不設置。

錯誤行為：

- `libmp4v2.dll` 缺失：UI 提示並返回，`_info` 仍為 null，`LoadFile()` 返回 false。
- 文件路徑 null/空/不存在：`MP4File` constructor 拋 `ArgumentException`。
- native `MP4Read()` 返回零：`Chapters` 為 null，`Mp4Data` 保持 `Chapter = null`。
- 沒有章節時 native wrapper 會產生單章 fallback，因此 `Mp4Data.Chapter` 非空。
- `LoadMp4()` catch 只顯示錯誤，不重新拋；這可能導致 `LoadFile()` 因 `_info == null` 返回 false。
- `fsutil hardlink` 失敗不會直接拋；代碼靠 `File.Exists(linkedFile)` 決定是否回退。

## 4. 多片段、合併、追加與選擇 clip 的狀態語義

核心狀態：

- `_infoGroup` 是當前來源的可選片段集合，實際類型可能是 `MplsGroup`、`IfoGroup`、`XplGroup`、`BDMVGroup`。
- `_info` 是當前顯示/編輯/保存的 `ChapterInfo`。
- `ClipSelectIndex` 是 `comboBox2.SelectedIndex < 0 ? 0 : comboBox2.SelectedIndex`。
- `CombineChapter` 是 `combineToolStripMenuItem.Checked`，只對 MPLS/IFO 生效。

載入語義：

- MPLS：每個 `PlayItem` 一個 `ChapterInfo`，combo item 是 `{SourceName}__{ChapterCount}`。載入後選中 `ClipSelectIndex`，再按 `CombineChapter` 生成 `_info`。
- IFO：每個可用 PGC/title 一個 `ChapterInfo`，combo item 是 `{SourceName}__{ChapterCount}`。載入後 `_info = CombineChapter ? CombineChapter(_infoGroup) : _infoGroup.First()`。
- XPL：每個含 `ChapterList` 的 Title 一個 `ChapterInfo`，combo item 是 `{Title}__{ChapterCount}`。不支持合併。
- BDMV：每個 eac3to 輸出且有 chapters 的 playlist 一個 `ChapterInfo`，combo item 是 `{SourceName}__{ChapterCount}`。不支持合併。
- MP4：單個 `ChapterInfo`，沒有 `_infoGroup` 選擇語義。

選擇語義：

- `comboBox2_SelectionChangeCommitted()`：
  - `MplsGroup` 調 `GetChapterInfoFromMpls(index)`。
  - `IfoGroup` 調 `GetChapterInfoFromIFO(index)`。
  - 其他 group 直接 `_info = _infoGroup[index]`。
  - 若 `Shift` 已啟用，重新套用時間表達式。
  - 刷新表格。

合併語義：

- 合併只允許 `MplsGroup` 和 `IfoGroup`。
- `ChapterInfo.CombineChapter(source, type)`：
  - 建立 `Title = "FULL Chapter"`。
  - `SourceType = type`，MPLS 傳 `"MPLS"`，IFO 默認 `"DVD"`。
  - `FramesPerSecond = source.First().FramesPerSecond`。
  - 依 group 順序累加每段 `Duration` 作為下一段 offset。
  - 每段內每個章節時間變成 `durationOffset + chapter.Time`。
  - 章節序號與名稱重新生成，不保留原 chapter name。
  - `Duration` 是所有片段 duration 總和。
- 合併結果不設 `SourceName`，因此保存 MPLS/IFO 時若依賴 `_info.SourceName` 可能得到空值或 null 相關行為，需在 Avalonia 版明確定義。

追加 MPLS：

- 只在 `_infoGroup is MplsGroup` 時可用。
- 用文件選擇器選另一個 `.mpls`。
- 調用 `LoadMpls(setGlobal: false, addToComboBox: true, customPath: newFile)`：
  - 不替換 `_infoGroup`。
  - 會把新 MPLS 的每個 play item 追加到 combo box。
  - 返回新 `MplsGroup`。
- `_infoGroup.AddRange(mplsGroup)`。
- 強制 `CombineChapter = true`。
- 重新按當前 `ClipSelectIndex` 生成 `_info` 並刷新表格。
- 注意：追加後 `FilePath` 仍是原 MPLS，打開對應 m2ts 和保存基礎路徑仍以原文件為準。

打開對應視頻文件：

- 由片段下拉的 context menu 動態追加菜單項。
- MPLS：
  - 基準是當前 `.mpls` 所在目錄。
  - 目標目錄是 `..\STREAM`。
  - 從 combo text 中取 `SourceName`，按 `&` 分割 multi-angle clip，為每個 `{clip}.m2ts` 建菜單。
- IFO：
  - 從 combo text 取 `SourceName`，追加 `.VOB`。
  - 目標在 IFO 同目錄。
  - 現有 `SourceName` 形如 `VTS_05_5`，對應文件名會是 `VTS_05_5.VOB`；是否符合 DVD 實際 `VTS_05_1.VOB` 命名需驗證。
- XPL：
  - 基準是 `.xpl` 所在目錄。
  - 目標目錄是 `..\HVDVD_TS`。
  - 文件名取 `Path.GetFileName(_info.SourceName)`。
- 打開動作用 `Process.Start(path)`；錯誤彈窗並記錄 log。

## 5. 外部依賴與平台限制

eac3to：

- BDMV 解析依賴 `eac3to.exe`。
- 路徑通過 `RegistryStorage` 保存在 `chaptertool.json` 的 `eac3toPath`。
- Core 不應直接彈輸入框；Avalonia 應返回 `MissingDependency`，由 UI 問用戶或跳到設定頁。
- 目前通過父目錄工作路徑和相對 disc 目錄名規避部分路徑問題；非 ASCII 或過複雜路徑仍可能失敗。
- 測試應 mock 外部進程輸出，不依賴本機安裝 eac3to。

現有進程 runner：

- `TaskAsync.RunProcessAsync(file, args, workingDirectory)` 使用 `Process`，`UseShellExecute=false`、`CreateNoWindow=true`，stdout/stderr 都 redirect。
- 返回值只累積 stdout；stderr 被啟動讀取但不暴露，exit code 也不返回。
- 沒有 timeout、取消或重試；完成依賴 `Exited` 事件。
- Avalonia 應以 `IExternalProcessRunner` 返回 stdout、stderr、exit code、命令、工作目錄、timeout/cancellation 狀態。

libmp4v2 / Knuckleball：

- MP4 讀取依賴輸出目錄中的 `libmp4v2.dll`。
- `Knuckleball.NativeMethods` P/Invoke 固定 DLL 名 `libMP4V2.dll`，在 Windows 文件系統大小寫不敏感時可工作。
- 專案包含 `Time_Shift/mp4v2/x86/libmp4v2.dll` 和 `Time_Shift/mp4v2/x64/libmp4v2.dll`，舊 csproj 按平台複製為 `libmp4v2.dll`。
- 這是 Windows/native 依賴；Avalonia 跨平台版本需決定保留 native adapter、改用純 .NET MP4 parser，或將 MP4 支持設為可選插件。

分發與授權：

- `SharpDvdInfo/LICENSE`、根目錄 `LICENSE` 與 bundled `mp4v2` DLL 都是本模塊遷移輸入。
- 若保留 DVD/MP4 native 支持，發佈包必須保留對應第三方授權聲明、x86/x64 DLL 選擇與缺失依賴提示；若替換為純 .NET parser，需記錄依賴與授權變更。

硬鏈接：

- `LoadMp4()` 用 `fsutil hardlink create` 建臨時硬鏈接，目標位於原文件磁碟根目錄。
- `fsutil` 是 Windows 工具；建立硬鏈接通常要求同一卷，可能受權限、文件系統、路徑策略影響。
- 現有 `CreateHardLinkW` P/Invoke 存在但 MP4 流程使用的是 `CreateHardLinkCMD()`。
- Avalonia 應把「建立短路徑/兼容路徑」抽象成平台服務，失敗時可降級使用原路徑並提供診斷。

DVD/BD/HD-DVD 文件結構：

- MPLS 打開視頻假定 `.mpls` 在 `BDMV/PLAYLIST`，對應 m2ts 在相鄰 `../STREAM`。
- BDMV 目錄必須包含 `BDMV/PLAYLIST`，disc title 可選來自 `BDMV/META/DL/*.xml`。
- DVD 目錄模式假定存在 `VIDEO_TS/VIDEO_TS.IFO` 和 `VTS_nn_0.IFO`。
- IFO 文件模式要求 `VTS_nn_0.IFO` 命名。
- XPL 打開視頻假定 `.xpl` 附近存在 `../HVDVD_TS`。

其他平台限制：

- `Process.Start(path)`、Registry、WinForms notification、`fsutil`、`Kernel32.dll`、`shell32.dll` 都是 Windows/WinForms 邊界。
- MPLS/IFO/XPL 的純解析部分可以跨平台；BDMV/MP4 取決於外部工具/native 庫替代方案。

## 6. 對應測試與樣例文件

已覆蓋：

- `Time_Shift_Test/Util/MplsDataTests.cs`
  - `00011_eva.mpls`：單 play item、24fps code、PTS offset 和 mark 列表。
  - `00001_fch.mpls`：單 play item、24000/1001 fps code、mark list。
  - `00002_tanji.mpls`：9 個 play item、多 angle `FullName`、各 play item mark 分佈。
- `Time_Shift_Test/Util/IfoDataTests.cs`
  - `VTS_05_0.IFO` 經 `IfoData.GetStreams()` 得到 1 個結果、7 章，驗證章節名與時間。
- `Time_Shift_Test/Util/IfoParserTests.cs`
  - `BcdToInt()` 對所有 byte 輸出做枚舉檢查。
- `Time_Shift_Test/Util/IfoTimeTests.cs`
  - `IfoTimeSpan` NTSC frame 累計與 `TimeSpan` 轉換。
- `Time_Shift_Test/SharpDvdInfo/DvdInfoContainerTests.cs`
  - 同一 IFO 經 `DvdInfoContainer` 得到 DVD chapter list，驗證 8 個時間點。
- `Time_Shift_Test/Knuckleball/MP4FileTests.cs`
  - `Chapter.mp4` 經 `Mp4Data` 讀出 4 章，時間為 0/10/20/30 秒。

樣例：

- `Time_Shift_Test/[mpls_Sample]/00001_fch.mpls`
- `Time_Shift_Test/[mpls_Sample]/00002_tanji.mpls`
- `Time_Shift_Test/[mpls_Sample]/00011_eva.mpls`
- `Time_Shift_Test/[ifo_Sample]/VTS_05_0.IFO`
- `Time_Shift_Test/[Video_Sample]/Chapter.mp4`

缺口：

- 沒有 XPL 樣例與單元測試。
- 沒有 BDMV/eac3to mock 測試。
- 沒有無效 MPLS/IFO、缺 primary video stream、無 chapter mark、多 playlist append、combo 切換、合併後保存路徑測試。
- 沒有 `libmp4v2.dll` 缺失、native `MP4Read` 失敗、硬鏈接失敗測試。

## 7. Avalonia/Core 建議接口、平台服務與錯誤模型

建議 importer contract：

```csharp
public interface IChapterImporter
{
    string Id { get; }
    IReadOnlyCollection<string> SupportedExtensions { get; }
    ValueTask<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken);
}

public sealed record ImportRequest(
    string Path,
    bool IsDirectory,
    ImportOptions Options);

public sealed record ImportResult(
    ChapterInfoGroup? Group,
    ChapterInfo? Single,
    string? DisplayTitle,
    IReadOnlyList<ImportDiagnostic> Diagnostics);
```

建議 importer 拆分：

- `MplsChapterImporter`：純文件解析，輸出 `MplsGroup`。
- `IfoChapterImporter`：使用現有 `IfoData` 語義，另可提供 `DvdInfoImporter` 讀 metadata。
- `XplChapterImporter`：純 XML 解析，輸出 `XplGroup`。
- `BdmvChapterImporter`：依賴 `IExternalProcessRunner`、`ISettingsStore`、`IFileSystem`，輸出 `BDMVGroup` 和 disc title。
- `Mp4ChapterImporter`：依賴 `IMp4ChapterReader`、`INativeDependencyResolver`、`IPathCompatibilityService`。

建議平台服務：

- `IFileSystem`：讀檔、列目錄、刪除臨時文件、檢查 DVD/BD 結構。
- `IExternalProcessRunner`：執行 eac3to，返回 stdout/stderr/exit code。
- `ISettingsStore`：保存 eac3to 路徑，不在 Core 依賴 Registry。
- `IDependencyPrompt` 或 UI command：處理缺少 eac3to/libmp4v2 時的用戶交互。
- `IPathCompatibilityService`：MP4 硬鏈接/短路徑/臨時副本策略。
- `IShellLauncher`：打開對應 `.m2ts`、`.VOB`、HD-DVD source 文件。
- `ILogger` 或 `IImportDiagnosticSink`：替代 `static event Action<string> OnLog`。

建議錯誤模型：

```csharp
public enum ImportErrorCode
{
    UnsupportedPath,
    InvalidStructure,
    InvalidFileHeader,
    UnsupportedVersion,
    MissingDependency,
    DependencyExecutionFailed,
    DependencyOutputUnrecognized,
    NoChaptersFound,
    NativeLibraryMissing,
    NativeReadFailed,
    ParseFailed,
    PlatformFeatureUnavailable
}
```

建議規則：

- 可恢復問題放 `Diagnostics`，例如 unknown stream coding、BDMV playlist without chapters、hardlink fallback。
- 不能產生結果的問題返回失敗 result，不在 Core 直接彈窗。
- 缺少依賴要帶上 dependency id、目前查找路徑、可配置位置。
- 外部工具執行要保留 command、working directory、exit code、stdout 摘要，避免只顯示泛化錯誤。
- 多片段結果應明確標記是否支持合併、是否支持 append、是否支持打開 source media。

ViewModel 建議：

- `ObservableCollection<ChapterSourceOption>` 表示 combo box 項目，避免從 `{SourceName}__{ChapterCount}` 字符串反解析。
- `SelectedClipIndex` 改變時通過服務生成 `CurrentInfo`，而不是直接操作 combo text。
- `CombineChapter` 僅在 `CurrentGroupCapabilities.CanCombine` 時可用。
- `AppendMplsCommand` 僅在當前 group 是 MPLS 時可用；追加後保持原 group source context 與新增 MPLS source context，打開視頻時按各片段自己的來源路徑解析。
- `OpenSourceMediaCommand` 從 importer 提供的 `SourceMediaRef` 生成，不依賴 combo text substring。

## 8. 未確定或需測試驗證的點

- `IfoData.GetStreams()` 對 `VTS_nn_0.IFO` 每次迭代都把 `titleSetNum` 覆蓋成文件名中的 nn，導致多 PGC 遍歷可能重複同一結果；需用多 PGC IFO 驗證。
- `IfoData` 與 `SharpDvdInfo` 對同一樣例輸出章節數與時間不同，需決定 Avalonia 版以哪條路徑為 DVD 章節真值。
- `SharpDvdInfo.GetVmgmInfo()` 中 `GetTitleInfo(info.TitleNumber, ref info)` 是否應使用 `TitleSetNumber` 而不是 `TitleNumber`，需用完整 DVD 目錄樣例驗證。
- MPLS 無 primary video stream、frame rate code 為 0/5、mark 第一點早於 INTime、多 angle 打開文件等邊界未測。
- MPLS `SubPath` constructor 從 `i = 1` 開始讀 `SubPlayItems`，`SubPlayItems[0]` 會保留 null；目前章節解析不使用，但重寫時需確認是否 bug。
- BDMV regex 只覆蓋部分 eac3to 輸出格式；新版或不同語言輸出可能不匹配。
- BDMV `SourceType` 和 `FramesPerSecond` 未設置，表格刷新會把它當未知幀率來源；需定義預期幀率策略。
- BDMV 臨時 `chapters.txt` 固定寫在父目錄，並發載入或父目錄只讀時會失敗；重寫時應使用隔離臨時目錄。
- MP4 native chapter duration 單位依 `libmp4v2` struct 註釋視為毫秒，需用更多 MP4 樣例驗證 QuickTime/Nero 章節。
- MP4 fallback 單章結果是否應視為「無章節」還是「整片一章」需產品決策。
- 硬鏈接策略的原始目的可能是規避非 ASCII 或長路徑；若改用純 .NET parser 需回歸這些路徑樣例。
- IFO 打開 `.VOB` 文件名從 `SourceName` 直接加 `.VOB` 可能不符合實際 DVD VOB 命名，需要 UI 測試。
- 合併後 `SourceName` 丟失會影響 MPLS/IFO 保存文件名與 JSON `sourceName`，需明確新模型的合併來源命名。

## 9. 本模塊覆蓋的源文件列表

- `Time_Shift/Util/ChapterData/MplsData.cs`
- `Time_Shift/Util/ChapterData/IfoData.cs`
- `Time_Shift/Util/ChapterData/IfoParser.cs`
- `Time_Shift/Util/ChapterData/XplData.cs`
- `Time_Shift/Util/ChapterData/BDMVData.cs`
- `Time_Shift/Util/ChapterData/Mp4Data.cs`
- `Time_Shift/Util/ChapterData/StreamUtils.cs`
- `Time_Shift/Knuckleball/Chapter.cs`
- `Time_Shift/Knuckleball/IntPtrExtensions.cs`
- `Time_Shift/Knuckleball/MP4File.cs`
- `Time_Shift/Knuckleball/NativeMethods.cs`
- `Time_Shift/SharpDvdInfo/LICENSE`
- `Time_Shift/SharpDvdInfo/DvdInfoContainer.cs`
- `Time_Shift/SharpDvdInfo/DvdTypes/DvdAudio.cs`
- `Time_Shift/SharpDvdInfo/DvdTypes/DvdSubpicture.cs`
- `Time_Shift/SharpDvdInfo/DvdTypes/DvdVideo.cs`
- `Time_Shift/SharpDvdInfo/Model/AudioProperties.cs`
- `Time_Shift/SharpDvdInfo/Model/SubpictureProperties.cs`
- `Time_Shift/SharpDvdInfo/Model/TitleInfo.cs`
- `Time_Shift/SharpDvdInfo/Model/VideoProperties.cs`
- `Time_Shift/SharpDvdInfo/Model/VmgmInfo.cs`
- `Time_Shift/Forms/Form1.cs` 中 MPLS/IFO/XPL/BDMV/MP4 載入、追加、合併、片段選擇、打開對應視頻文件相關邏輯
- `Time_Shift/Util/NativeMethods.cs` 中 MP4 硬鏈接相關邏輯
- `Time_Shift/Util/TaskAsync.cs` 中 BDMV/eac3to 進程執行相關邏輯
- `Time_Shift/Util/ChapterInfo.cs` 中 `CombineChapter()` 與 MPLS JSON `SourceName` 行為
- `Time_Shift/Util/ChapterInfoGroup.cs` 中 `MplsGroup`、`IfoGroup`、`XplGroup`、`BDMVGroup`
- `Time_Shift/Util/Chapter.cs`
