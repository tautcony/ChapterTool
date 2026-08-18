# 模塊 01：主窗口 UI 與用戶交互

## 1. 模塊目的與 Avalonia 重寫邊界

本模塊描述 ChapterTool 主窗口的啟動、可見控件、快捷鍵、拖放、上下文菜單、狀態欄與章節表格交互。Avalonia 重寫時，本模塊只負責 UI shell、命令入口、狀態呈現與 ViewModel 綁定契約；章節解析、保存格式生成、幀率推斷、表達式計算、BDMV/MP4/Matroska 等具體格式處理應留給核心服務模塊。

重寫邊界：

- 保留現有主窗口工作流：載入來源、選擇片段、刷新表格、編輯章節、預覽、保存、打開日誌、展開高級面板。
- 將 WinForms 事件處理器拆成 Avalonia `ICommand`、可觀察屬性和對話框/通知服務。
- 不在 Avalonia View 或 ViewModel 中直接依賴 `DataGridView`、`ToolStrip`、`Notification`、`Application.DoEvents()`；配置文件、Windows registry 操作與系統菜單需以平台服務或狀態模型替代。
- 本模塊不重新設計解析器、導出器、更新檢查、關於窗口、顏色窗口、預覽窗口內部 UI，但需要保留主窗口打開它們的命令入口。

## 2. 審閱過的文件清單

主要範圍：

- `Time_Shift/Program.cs`
- `Time_Shift/Forms/Form1.cs`
- `Time_Shift/Forms/Form1.Designer.cs`
- `Time_Shift/Forms/Form1.resx`
- `Time_Shift/Forms/Form1.en-US.resx`

輔助查閱：

- `Time_Shift/Properties/Resources.resx`
- `Time_Shift/Properties/Resources.en-US.resx`
- `Time_Shift/Util/LanguageSelectionContainer.cs`

## 3. 啟動與主窗口狀態

`Program.Main` 啟動前先啟用 WinForms 視覺樣式並檢查 .NET Framework 4.6.2 release key。現有門檻是 `Release >= 394802`；版本不足時顯示 `Message_Need_Newer_dotNet`，打開下載頁；若用戶選擇不再提醒，通過 `RegistryStorage.Save(false, "DoVersionCheck")` 寫入可執行文件目錄下的 `chaptertool.json`。

啟動參數行為：

- 無參數：`Application.Run(new Forms.Form1())`。
- 有參數：只跳過參數列表開頭連續出現的 `--`；一旦遇到第一個非 `--` 參數，後續 `--` 會被當作普通路徑候選。剩餘參數以空格合併為路徑，傳入 `Form1(string args)`，窗口載入後嘗試載入該文件。

`Form1` 初始化行為：

- 無參構造會通過 `RegistryStorage.Load<string>("Language")` 讀取可執行文件目錄下 `chaptertool.json` 中的語言設定，並套用 `Form1` 語言資源。
- 兩個構造函數都調用 `InitializeComponent()`、`AddCommand()`，並使用當前可執行文件圖標。
- `Form1_Load` 設置 DPI 尺寸、窗口標題 `[VCB-Studio] ChapterTool v{version}`、初始化日誌、恢復窗口位置、填充保存格式與 XML 語言、插入幀數取整誤差菜單、清空默認狀態、載入顏色、收起高級面板、默認保存類型 `TXT`、設置刷新按鈕文字為 `↺` 或 `↻`。
- 若已有 `FilePath`，載入成功後刷新表格。

初始 UI 狀態：

- `comboBox2` 片段選擇禁用且隱藏。
- `comboBox1` 幀率選擇未選中。
- `cbShift` 未勾選。
- `_infoGroup`、`_info`、`_bdmvTitle` 為空。
- 章節表格清空。
- 高級面板 `panel1` 隱藏，窗口高度為收起高度。

## 4. UI 控件清單

| 控件 | 可見文本 | 初始/狀態 | 事件與命令 | 快捷鍵 | 上下文菜單/提示 |
| --- | --- | --- | --- | --- | --- |
| `btnLoad` | 中文：`載入`；英文：`Load` | 常啟用 | 左鍵 `btnLoad_Click` 打開文件選擇器 | `Ctrl+O` | 右鍵 `loadMenuStrip`：`重新載入/Reload file`、`追加合併/Append file` |
| `btnSave` | `保存/Save` | 常啟用，但保存前需有效來源 | 左鍵 `SaveFile(SelectedSaveType)`；右鍵選擇自定義保存目錄 | `Ctrl+S`、`Alt+S` | 懸停時對疑似無效 MPLS 第二章節提示 `ToolTips_Useless_Chapter` |
| `lbPath` | 顯示當前文件名；未載入時用 `File_Unloaded` | 長文件名中間省略 | 懸停顯示完整路徑 | 無 | Tooltip 為完整 `FilePath` |
| `btnTrans` | `↺` 或 `↻` | 常啟用 | 左鍵刷新表格；右鍵打開顏色窗口 | `Ctrl+R`、`F5` | 無 |
| `comboBox1` | 幀率：`24000 / 1001`、`24000 / 1000`、`25000 / 1000`、`30000 / 1001`、`RESER / VED`、`50000 / 1000`、`60000 / 1001` | MPLS/DVD 載入後禁用；其他來源可選 | 選擇後按索引刷新幀數 | 無 | 無 |
| `cbRound` | `幀數取整 / Round` | 默認勾選 | 勾選變化刷新表格 | 無 | 右鍵 `deviationMenuStrip`：`誤差範圍/Tolerance scope`，項目 `0.01`、`0.05`、`0.10`、`0.15`、`0.20`、`0.25`、`0.30`，默認 `0.15` |
| `comboBox2` | 動態片段項，如 `{SourceName}__{ChapterCount}`、`Edition NN` | 默認隱藏禁用；有片段組後顯示 | 選擇片段並刷新 `_info` 與表格 | `PageUp`、`PageDown`、`Ctrl+0..9` | 右鍵 `combineMenuStrip`：`合併章節/Merge chapter`，並動態追加 `打開/Open {file}` 項 |
| `dataGridView1` | 列：`#`、`時間點/Time Stamp`、`章節名/Chapter Name`、`幀數/Frames` | 不允許用戶添加行；`#` 只讀；不可排序 | 編輯結束、刪除行、右鍵行菜單 | Delete 為 DataGridView 默認刪除 | 右鍵行 `createZonestMenuStrip`：`生成Zones/Create Zones`、`向前平移/Forward translation`、`插入新章節/Insert new Chapter` |
| `btnPreview` | `P` | 常啟用，但預覽需有效來源 | 左鍵打開/更新預覽窗口；右鍵管理員模式關聯 `.mpls` | 無 | 右鍵提示 `Message_Open_With_CT` |
| `statusStrip1` | 狀態欄 | 常顯示 | 包含狀態文字、進度條、展開按鈕 | `F11` | 進度條連點可打開 About，屬隱藏行為 |
| `tsTips` | 動態狀態文字 | 空或提示文本 | 顯示載入、保存、表達式、越界等提示 | 無 | 無 |
| `tsProgressBar1` | 進度條 | `0` 起 | 單擊計數；前 1-2 次提示 `Something happened`，達門檻打開 About，之後門檻增加 10 | 無 | 隱藏彩蛋行為 |
| `tsBtnExpand` | 圖標按鈕 | 初始收起圖標 | 展開/收起高級面板 | `F11` | 無 |
| `savingType` | `TXT`、`XML`、`QPF`、`TimeCodes`、`TsmuxerMeta`、`CUE`、`JSON` | 默認 `TXT` | 選擇變化控制 XML 語言下拉啟用狀態 | `Alt+0..9` | 無 |
| `xmlLang` | 由 `LanguageSelectionContainer.LoadLang` 填充 | 僅 `XML` 保存類型啟用；初次啟用默認索引 `2` | 選中索引 `0` 會改為 `2`，索引 `5` 會改為 `6` | 無 | 無 |
| `lbFormat` | `保存格式/Type` | 高級面板內 | 靜態標籤 | 無 | 無 |
| `lbXmlLang` | `XML語言/Lang` | 高級面板內 | 靜態標籤 | 無 | 無 |
| `cbAutoGenName` | 中文：`不使用章節名`；英文：`Auto name` | 默認未勾選 | 勾選變化以自動章節名刷新表格顯示/導出 | 無 | Tooltip：從 `Chapter 01` 開始重命名 |
| `cbChapterName` | `使用章節名模板/Use Template` | 默認未勾選 | 勾選時打開文本文件並載入模板；取消時清空模板 | 無 | Tooltip：不取消勾選時持續生效 |
| `lbShift` | `平移章節號/Shift Order` | 高級面板內 | 靜態標籤 | 無 | 無 |
| `numericUpDown1` | 無文本 | 範圍 `0..50` | 改變後更新章節序號並刷新表格 | 無 | 無 |
| `cbShift` | `應用表達式 (t)/Apply expr (t)` | 默認未勾選 | 勾選時解析 `comboBoxExpression.Text` 並刷新 | 無 | 無 |
| `comboBoxExpression` | 默認項由資源提供，至少包含兩個表達式 | 可輸入 | 文本變化驗證；選擇變化重新應用表達式 | `Ctrl+PageUp`、`Ctrl+PageDown` | 狀態欄顯示 `Valid expression` 或 `Invalid expression` |
| `cbPostFix` | 無可見文字 | 默認未勾選 | 作為 `ParseExpression()` 的被動選項，控制下一次表達式解析按中綴或逆波蘭解析 | 無 | Tooltip：`逆波蘭表達式/Inverse Polish expression`；自身沒有 `CheckedChanged` 命令入口 |
| `btnLog` | `LOG` | 常啟用 | 打開/聚焦日誌窗口 | `Ctrl+L` | 無 |
| 系統菜單命令 | `Switch language` / `切換語言` | 僅非 Mono 且存在 `en-US` 資源目錄時添加 | 切換 `chaptertool.json` 中的 `Language`，重啟程序並結束當前進程 | 無 | 缺少資源 DLL 時提示 `No valid language resource file found` |

## 5. 用戶命令行為

### 載入文件

入口：`btnLoad`、`Ctrl+O`。

前置條件：無。文件選擇器使用支持格式過濾器：`.txt`、`.xml`、`.mpls`、`.ifo`、`.xpl`、`.cue`、`.tak`、`.flac`、`.mkv`、`.mka`、`.mp4`、`.m4a`、`.m4v`、`.vtt`。

狀態變化：

- 用戶選擇文件後設置 `FilePath`，清空 `comboBox2.Items`。
- `LoadFile()` 先校驗副檔名；非法時清空 `FilePath`，`lbPath` 顯示 `File_Unloaded`，狀態欄顯示 `Tips_InValid_Type`。
- 載入成功後 `lbPath` 顯示文件名，超過 55 字符時中間省略。
- 按來源類型建立 `_info` 或 `_infoGroup`；多片段來源填充 `comboBox2`。
- `UpdateGridView()` 刷新表格；進度一般到 `33` 或 `66`，狀態為 `Tips_Load_Success` 或 `Tips_Chapter_Not_find`。

錯誤/提示：

- `LoadFile()` 捕獲異常時彈出錯誤，記錄日誌，清空 `FilePath`，進度歸 `0`，`lbPath` 顯示 `File_Unloaded`。
- MP4 缺少 `libmp4v2.dll` 時提示是否前往下載頁。
- Matroska 無章節時顯示信息；其他錯誤顯示錯誤通知。

### 通過拖放載入

入口：主窗口 `DragEnter`、`DragDrop`。

前置條件：拖放數據包含 `FileDrop`。

狀態變化：

- `DragEnter` 將效果設為 `Copy`，否則 `None`。
- `DragDrop` 取第一個路徑為 `FilePath`。
- 若路徑為目錄，視為 BDMV，設置 `_isUrl=true` 並調用 `LoadBDMVAsync()`。
- 若為文件，設置 `_isUrl=false`，校驗類型，清空片段下拉，載入後刷新。

錯誤/提示：

- BDMV 載入中狀態為 `Tips_Loading`。
- BDMV 無結果時狀態為 `Tips_Load_Fail`。
- BDMV 異常時顯示 `Exception thrown while loading BluRay disc`。

### 通過命令行參數載入

入口：`Program.Main(args)`。

前置條件：至少存在一個非 `--` 選項參數。

狀態變化：

- 參數合併成完整路徑傳入 `Form1(string args)`。
- 構造函數設置 `FilePath` 並記錄日誌。
- `Form1_Load` 中若 `FilePath` 非空，載入並刷新表格。

錯誤/提示：同載入文件。

### 重新載入

入口：`btnLoad` 右鍵菜單 `重新載入/Reload file`。

前置條件：`FilePath` 非空。

狀態變化：

- BDMV 來源重新執行 `LoadBDMVAsync()`。
- 文件來源重新 `LoadFile()`，成功後刷新表格。

錯誤/提示：同對應載入流程。

### 追加合併 MPLS

入口：`btnLoad` 右鍵菜單 `追加合併/Append file`。

前置條件：當前 `_infoGroup` 必須是 `MplsGroup`。

狀態變化：

- 打開 `.mpls` 文件選擇器，初始目錄為當前文件所在目錄。
- 新 MPLS 以 `setGlobal:false`、`addToComboBox:true` 載入並追加到 `_infoGroup`。
- 設置 `CombineChapter=true`，重新生成當前 MPLS 章節並刷新表格。

錯誤/提示：不符合前置條件時直接返回；載入錯誤由 MPLS 載入流程處理。

### 保存

入口：`btnSave` 左鍵、`Ctrl+S`、`Alt+S`。

前置條件：`FilePath` 有效且 `_info` 可用；`savingType.SelectedIndex` 可映射到保存類型。

狀態變化：

- 保存前調用 `UpdateGridView()`，根據保存類型生成唯一保存路徑。
- 默認保存目錄為來源文件所在目錄；若右鍵保存設置過 `_customSavingPath`，使用該目錄。
- MPLS/IFO 導出文件名追加 `__{_info.SourceName}`。
- 保存成功後進度為 `100`，狀態為 `Tips_Save_Success`。
- `Alt+S` 會保存當前片段，若存在下一片段則切到下一片段並刷新；切到最後一片段時再保存一次。

錯誤/提示：

- 無有效來源時 `IsPathValid` 設置 `File_Unloaded` 或非法類型提示並返回。
- 保存異常彈出錯誤，記錄日誌，進度為 `60`，狀態為 `Tips_Save_Fail`。

### 設置保存目錄

入口：`btnSave` 右鍵。

前置條件：用戶在目錄選擇器確認。

狀態變化：

- `_customSavingPath` 設為所選目錄。
- 通過 `RegistryStorage.Save(_customSavingPath)` 記住路徑；雖然類名含 `Registry`，實際持久化目標是可執行文件目錄下的 `chaptertool.json`，不是 Windows registry。

錯誤/提示：異常時彈錯誤通知，清空 `_customSavingPath`。

### 刷新表格

入口：`btnTrans` 左鍵、`Ctrl+R`、`F5`、`cbRound` 切換、幀率選擇、片段選擇、部分高級選項變化。

前置條件：`FilePath` 有效且 `_info` 非空。

狀態變化：

- MPLS/DVD 使用來源已知幀率並禁用幀率下拉。
- 其他來源根據下拉或自動偵測更新 `_info.FramesPerSecond`，並啟用幀率下拉。
- 表格行數變化或插入新行時清空重建；否則就地更新行。
- 表格多於一行時進度為 `66`，否則 `33`。

錯誤/提示：無有效來源時由 `IsPathValid` 顯示 `File_Unloaded` 或非法類型提示。

### 打開顏色設置

入口：`btnTrans` 右鍵。

前置條件：無。

狀態變化：懶加載 `FormColor(this)`，顯示、聚焦並選中。

錯誤/提示：只記錄打開日誌；Avalonia 可作為非核心或外觀設置模塊處理。

### 選擇片段

入口：`comboBox2` 選擇、`PageUp`、`PageDown`、`Ctrl+0..9`。

前置條件：`comboBox2.Items` 存在對應索引。

狀態變化：

- MPLS：按 `CombineChapter` 狀態取單片段或合併章節。
- IFO：按 `CombineChapter` 狀態取單片段或合併章節。
- 其他分組：直接取 `_infoGroup[ClipSelectIndex]`。
- 若 `Shift` 已啟用，重新應用表達式。
- 刷新表格。

錯誤/提示：

- `Ctrl+0..9` 超出範圍時狀態欄顯示 `Tips_Out_Of_Range`。
- 片段懸停顯示片段數；對疑似播放菜單 MPLS 顯示 `Tips_Menu_Clip`。

### 合併章節

入口：`comboBox2` 右鍵菜單 `合併章節/Merge chapter`。

前置條件：當前 `_infoGroup` 是 `MplsGroup` 或 `IfoGroup`。

狀態變化：切換 `CombineChapter`，重新選擇當前片段並刷新。

錯誤/提示：非 MPLS/IFO 直接返回。

### 打開對應視頻文件

入口：`comboBox2` 右鍵菜單動態 `打開/Open {file}` 項。

前置條件：

- MPLS：當前 MPLS 所在目錄的 `..\STREAM` 存在。
- IFO：可按當前片段名推導 `.VOB`。
- XPL：當前 XPL 所在目錄的 `..\HVDVD_TS` 存在。

狀態變化：使用系統關聯程序 `Process.Start(path)` 打開文件。

錯誤/提示：打開失敗時顯示錯誤通知並記錄日誌。

### 編輯章節表格

入口：表格單元格編輯結束。

前置條件：編輯行索引有效，行 `Tag` 為 `Chapter`。

狀態變化：

- 編輯第 1 列時間點：嘗試以 `TimeSpan.TryParse` 解析；超過一天則重置為 `TimeSpan.Zero`，否則更新章節時間。
- 編輯第 2 列章節名：記錄重命名日誌並更新 `Chapter.Name`。
- 編輯第 3 列幀數：提取數字，按當前幀率換算時間；超過一天則重置為零。
- 編輯後異步刷新表格。

錯誤/提示：解析失敗時保留或重算為既有狀態；現有代碼不彈出錯誤。

### 刪除章節行

入口：表格默認刪除行交互。

前置條件：刪除行對應 `_info.Chapters` 中的 `Chapter`。

狀態變化：

- 從 `_info.Chapters` 移除章節。
- 按 `numericUpDown1.Value` 重算序號。
- 若刪除第一行且仍有章節，使用新的第一章節時間重新校正初始時間。
- MPLS/IFO 且無自定義章節名模板時重新生成章節名。
- `RowsRemoved` 記錄刪除行數與索引。

錯誤/提示：無顯式確認與錯誤提示。

### 生成 Zones

入口：表格右鍵菜單 `生成Zones/Create Zones`。

前置條件：至少選中一行。

狀態變化：

- 強制 `cbRound.Checked=true`。
- 對每個選中行取當前行幀數為起點、下一行幀數減一為終點；最後一行使用自身作為下一行。
- 合併為 `--zones start,end,...` 字符串。
- 顯示信息並詢問是否複製到剪貼板；確認後寫入剪貼板。

錯誤/提示：未選中行時直接返回；現有代碼假設 `FramesInfo` 含空格分隔數字。

### 向前平移幀

入口：表格右鍵菜單 `向前平移/Forward translation`。

前置條件：有效來源，`comboBox1.SelectedIndex + 1 >= 1`。

狀態變化：

- 彈出輸入框，提示「向前平移N幀，小於0的將被刪除」。
- 輸入可解析為整數時，按當前幀率換算為時間偏移。
- `_info.UpdateInfo(shiftTime)` 後刪除時間小於零的章節，刷新表格。

錯誤/提示：輸入不可解析或無有效幀率時直接返回。

### 插入新章節

入口：表格右鍵菜單 `插入新章節/Insert new Chapter`。

前置條件：恰好選中一行。

狀態變化：

- 在選中行索引插入 `Chapter("New Chapter", TimeSpan.Zero, 0)`。
- 重算序號，設置 `_newRowInserted=true`，刷新表格。

錯誤/提示：選中行數不是 1 時直接返回。

### 預覽

入口：`btnPreview` 左鍵。

前置條件：有效來源。

狀態變化：

- 懶加載 `FormPreview(this)`，`TopMost=true`。
- 使用 `_info.GetText(AutoGenName)` 更新預覽文本。
- 顯示、聚焦並選中預覽窗口。

錯誤/提示：無有效來源時由 `IsPathValid` 提示。

### 註冊 `.mpls` 打開方式

入口：`btnPreview` 右鍵。

前置條件：右鍵，且 `RunAsAdministrator()` 為真。

狀態變化：提示 `Message_Open_With_CT`；用戶確認後寫入 `.mpls` 文件關聯。

錯誤/提示：非管理員或取消時直接返回。

### 展開/收起高級面板

入口：`tsBtnExpand`、`F11`。

前置條件：當前高度等於收起或展開目標高度。

狀態變化：

- 在兩個目標高度之間以每次 2 像素動畫切換。
- `panel1.Visible` 跟隨展開狀態。
- 圖標在 `arrow_drop_down`、`arrow_drop_up`、`unfold_more` 間切換。

錯誤/提示：高度不在目標值時直接返回。

### 選擇保存格式

入口：`savingType` 下拉、`Alt+0..9`。

前置條件：索引可映射到 `SaveTypeEnum`。

狀態變化：

- 保存格式為 `XML` 時啟用 `xmlLang`；若未選語言，設為索引 `2`。
- 非 `XML` 時禁用 `xmlLang`。

錯誤/提示：現有快捷鍵映射對 `Alt+0` 和越界索引需後續驗證。

### 選擇 XML 語言

入口：`xmlLang` 下拉。

前置條件：`savingType` 為 `XML`。

狀態變化：

- 選中索引 `0` 時改為 `2`。
- 選中索引 `5` 時改為 `6`。
- 保存 XML 時從選中項括號中提取語言名，再映射到 ISO 代碼。

錯誤/提示：未見顯式錯誤提示。

### 使用章節名模板

入口：`cbChapterName`。

前置條件：勾選時用戶選擇文本文件。

狀態變化：

- 勾選：打開文本文件選擇器，讀取 UTF 字符串作為 `_chapterNameTemplate`。
- 取消或選擇器取消：清空模板或取消勾選。
- 有效來源下調用 `_info.UpdateInfo(_chapterNameTemplate)` 並刷新表格顯示。

錯誤/提示：讀取失敗時彈出錯誤通知並記錄日誌。

### 自動章節名顯示/導出

入口：`cbAutoGenName`。

前置條件：無。

狀態變化：刷新表格；導出時傳入 `AutoGenName`，使用 `Chapter NN` 類型章節名而非原始章節名。

錯誤/提示：無。

### 應用時間表達式

入口：`cbShift`、`comboBoxExpression` 選擇、`comboBoxExpression` 文本變化；`cbPostFix` 僅在這些入口觸發 `ParseExpression()` 時被讀取，現有 Designer 沒有為它綁定獨立事件。

前置條件：表達式文本可解析；解析當下若 `cbPostFix.Checked` 為 true，按逆波蘭表達式解析。

狀態變化：

- 文本變化只做正則驗證，狀態欄顯示 `Valid expression` 或 `Invalid expression`。
- 勾選應用後，若有有效 `_info`，設置 `_info.Expr` 為解析結果；取消時設為 `Expression.Empty`。
- 刷新表格幀數與時間顯示。

錯誤/提示：解析失敗時記錄日誌，使用 `Expression.Empty`，狀態欄顯示 `Tips_Invalid_Shift_Time`。

### 平移章節序號

入口：`numericUpDown1`。

前置條件：有效來源。

狀態變化：按數值更新章節序號並刷新表格，不更新幀信息。

錯誤/提示：無有效來源時返回。

### 打開日誌窗口

入口：`btnLog`、`Ctrl+L`。

前置條件：無。

狀態變化：懶加載 `FormLog`，顯示、聚焦並選中。

錯誤/提示：無。

### 切換語言

入口：系統菜單命令 `Switch language` / `切換語言`。

前置條件：非 Mono；可執行文件目錄下存在 `en-US` 目錄。

狀態變化：

- 若缺少 `ChapterTool.resources.dll`，顯示信息並返回。
- 在 `chaptertool.json` 的 `Language` 中於空值和 `en-US` 間切換。
- 啟動新進程並殺掉當前進程。

錯誤/提示：缺少資源 DLL 時提示 `No valid language resource file found`。

### 關閉主窗口

入口：窗口關閉。

前置條件：無。

狀態變化：

- 保存窗口 `Location` 到 `Software\ChapterTool`。
- 若進度條點擊彩蛋計數處於特定範圍，執行窗口移動或淡出動畫。

錯誤/提示：無。

## 6. 需要映射到 ViewModel 的命令/屬性

命令：

- `LoadFileCommand`
- `ReloadCommand`
- `AppendMplsCommand`
- `LoadDroppedPathCommand`
- `SaveCommand`
- `SetSaveDirectoryCommand`
- `RefreshCommand`
- `OpenColorSettingsCommand`
- `SelectClipCommand`
- `ToggleCombineChaptersCommand`
- `OpenRelatedMediaCommand`
- `EditChapterCellCommand`
- `DeleteChapterCommand`
- `CreateZonesCommand`
- `ShiftFramesForwardCommand`
- `InsertChapterCommand`
- `PreviewCommand`
- `RegisterMplsAssociationCommand`
- `ToggleAdvancedPanelCommand`
- `SelectSaveTypeCommand`
- `SelectXmlLanguageCommand`
- `LoadChapterNameTemplateCommand`
- `ToggleAutoGeneratedNamesCommand`
- `ApplyExpressionCommand`
- `ValidateExpressionCommand`
- `ShiftChapterOrderCommand`
- `OpenLogCommand`
- `SwitchLanguageCommand`
- `CloseWindowCommand`

屬性：

- `WindowTitle`
- `CurrentPath`
- `DisplayPath`
- `IsDirectorySource`
- `IsPathValid`
- `SupportedFileFilters`
- `ChapterRows`
- `SelectedChapterRows`
- `ClipItems`
- `SelectedClipIndex`
- `IsClipSelectorVisible`
- `IsClipSelectorEnabled`
- `CombineChapters`
- `FrameRateItems`
- `SelectedFrameRateIndex`
- `IsFrameRateEnabled`
- `RoundFrames`
- `FrameToleranceItems`
- `SelectedFrameTolerance`
- `SaveTypeItems`
- `SelectedSaveType`
- `XmlLanguageItems`
- `SelectedXmlLanguage`
- `IsXmlLanguageEnabled`
- `AutoGenerateNames`
- `UseChapterNameTemplate`
- `ChapterNameTemplateText`
- `ChapterOrderShift`
- `ApplyExpression`
- `ExpressionText`
- `ExpressionItems`
- `UsePostfixExpression`
- `IsExpressionValid`
- `CustomSaveDirectory`
- `StatusText`
- `ProgressValue`
- `IsAdvancedPanelVisible`
- `ExpandCollapseIconState`
- `CanPreview`
- `CanSave`
- `CanAppendMpls`
- `CanMergeChapters`
- `CanCreateZones`
- `CanInsertChapter`
- `CanShiftFramesForward`
- `RelatedMediaMenuItems`

服務接口建議：

- `IFileDialogService`：打開文件、打開文本模板、選擇保存目錄。
- `INotificationService`：信息、錯誤、確認、文本輸入。
- `IClipboardService`：複製 zones。
- `IProcessService`：打開 URL、打開相關視頻、重啟程序。
- `IRegistrySettingsService`：語言、窗口位置、保存目錄、文件關聯。
- `IWindowService`：預覽、日誌、顏色、關於窗口。
- `IChapterLoadService`、`IChapterSaveService`、`IFrameInfoService`、`IExpressionService`：承接非 UI 業務邏輯。

## 7. 未確定或需後續測試驗證的點

- `Alt+0..9` 保存格式快捷鍵現有代碼使用 `index - 1` 設置 `SelectedIndex`，`Alt+0` 會得到 `-1`，且邊界判斷使用 `index > savingType.Items.Count`；Avalonia 重寫前需確認實際期望。
- `Ctrl+0..9` 片段快捷鍵使用 `Ctrl+1` 對應第一項、`Ctrl+0` 對應第十項；需在新 UI 中明確提示或保留隱式行為。
- `xmlLang` 選中索引 `0 -> 2`、`5 -> 6` 的原因未在主窗口代碼中說明，需要結合語言列表驗證。
- `Form1_DragDrop` 對多文件拖放只使用 `_paths[0]`，但 `_paths` 是長度 20 的陣列；是否需要支持多文件仍待確認。
- `Create Zones` 對最後一行用自身作為終點，會產生 `endFrames - 1` 小於起點的風險；需用實際用例驗證。
- 表格時間與幀數編輯解析失敗時沒有用戶提示，是否保留靜默行為需產品決策。
- `FrameShiftForward` 使用中文硬編碼輸入框文本，不在資源文件中；Avalonia 本地化時需補資源鍵。
- 進度條點擊打開 About 與關閉窗口動畫屬彩蛋行為；是否遷移需單獨決策。
- `AddCommand` 中系統菜單語言切換依賴 Windows 系統菜單；Avalonia 跨平台需要替代入口或僅 Windows 啟用。
- 右鍵保存目錄、窗口位置、語言切換和 `DoVersionCheck` 依賴 `RegistryStorage`，但其現有實作是 `chaptertool.json` 配置文件；真正的 Windows registry 只用於 .NET runtime release key 檢查、MKVToolNix 探測和 `.mpls` 文件關聯。跨平台存儲方案需明確替代。
- `Application.DoEvents()` 驅動的刷新/動畫需改為異步任務、UI dispatcher 或無動畫狀態切換。
- `.mpls` 文件關聯只適合 Windows 且需管理員權限；Avalonia 版本應隔離到平台服務。

## 8. 本模塊覆蓋的源文件列表

- `Time_Shift/Program.cs`
- `Time_Shift/Forms/Form1.cs`
- `Time_Shift/Forms/Form1.Designer.cs`
- `Time_Shift/Forms/Form1.resx`（包含 WinForms layout/metadata 與主窗口文本資源）
- `Time_Shift/Forms/Form1.en-US.resx`（英文局部本地化資源）
