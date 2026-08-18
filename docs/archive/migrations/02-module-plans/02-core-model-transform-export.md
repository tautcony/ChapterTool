# 模塊 02：核心章節模型、時間/幀率/表達式、導出格式

本文檔記錄 ChapterTool Avalonia 重寫中「核心章節模型、時間/幀率/表達式、導出格式」模塊的既有 WinForms 行為與建議拆分邊界。目標是把目前散落在 `Util` 模型類和 `Form1.cs` 事件處理器中的規則抽成可測試的 Core 服務，再由 Avalonia ViewModel 調用；不把 WinForms 控件、`DataGridViewRow.Tag`、`ComboBox.SelectedIndex` 等 UI 狀態帶入 Core。

## 1. 模塊目的與重寫邊界

本模塊負責：

- 表示一組章節、單個章節、章節來源和片段集合。
- 在章節時間、幀數、幀率、取整容差、時間平移表達式之間轉換。
- 支持章節表格編輯、刪除、插入、序號平移、章節名模板和自動命名。
- 生成 TXT/OGM、Matroska XML、QPF、TimeCodes、tsMuxeR meta、CUE、JSON 七種保存格式。
- 提供 XML 語言代碼表和雙向字典等輔助核心資料結構。

Avalonia/Core 邊界：

- Core 保留模型、轉換、導出、表達式、幀率和章節編輯規則。
- Avalonia ViewModel 負責選中項、命令、校驗訊息、進度和 UI 顯示狀態。
- Infrastructure 負責文件系統寫入、保存路徑避重名、配置持久化、外部對話框和剪貼板。
- 不應在 Core 中引用 `System.Windows.Forms`、`DataGridView`、`Color`、`ComboBox` 或 WinForms 資源。

## 2. 審閱過的文件清單

核心範圍：

- `Time_Shift/Util/Chapter.cs`
- `Time_Shift/Util/ChapterInfo.cs`
- `Time_Shift/Util/ChapterInfoGroup.cs`
- `Time_Shift/Util/ChapterName.cs`
- `Time_Shift/Util/Expression.cs`
- `Time_Shift/Util/ToolKits.cs` 中的時間格式、時間解析、編碼偵測、保存路徑輔助等 Core 可遷移部分
- `Time_Shift/Util/LanguageSelectionContainer.cs`
- `Time_Shift/Util/DualDictionary.cs`
- `Time_Shift/Forms/Form1.cs` 中保存、幀數、表達式、表格編輯、刪除、插入、章節名模板相關邏輯

輔助確認：

- `Time_Shift/Util/ChapterData/MplsData.cs` 的 `FrameRate` 表
- `Time_Shift/Forms/Form1.Designer.cs`、`Form1.resx` 中幀率、表達式、序號平移、取整控件初始化
- `Time_Shift_Test/Util/ExpressionTests.cs`
- `Time_Shift_Test/Util/ToolKitsTests.cs`

`ToolKits.cs` 拆分邊界：本模塊只把 `Time2String`、`ToTimeSpan`、`ToCueTimeStamp`、`GetUTFString`、`FindProperFilePath` 等時間/編碼/保存語義作為 Core 契約記錄；顏色、進程、窗口定位、配置持久化、Windows registry 和文件關聯等平台相關 helper 由模塊 06 主覆蓋。

## 3. 模型字段與不變式

### `Chapter`

- `Number: int`：章節序號。保存 TXT/OGM 時使用 `D2` 格式；表格刪除、序號平移會重排為 `1 + shift`、`2 + shift`...
- `Time: TimeSpan`：章節原始時間。`TimeSpan.MinValue` 被用作特殊分隔標記，導出時多數格式會跳過，JSON 會特殊處理。
- `Name: string`：章節名。自動命名只影響顯示和導出，不修改原字段。
- `FramesInfo: string`：當前幀率和取整策略下的幀數顯示，例如 `1234 K`、`1234 *` 或未取整小數。

不變式與注意點：

- 普通章節應有非負時間；表格時間編輯若大於 1 天會被重置為 `TimeSpan.Zero`。
- `Number == -1` 在 WinForms 表格中被當作黑色分隔行，但核心導出主要依賴 `TimeSpan.MinValue` 分隔。
- `IsAccuracy(fps, accuracy, expr)` 以 `time * fps` 或 `expr(time, fps) * fps` 計算幀數，使用 `MidpointRounding.AwayFromZero` 取整，差值嚴格小於容差時返回 `1`。

### `ChapterInfo`

- `Title: string`：章節標題，CUE 的 `TITLE` 使用它。
- `SourceName: string`：對應片段或來源名。`.mpls`、`.ifo` 保存路徑會附加它；MPLS JSON 會輸出 `{SourceName}.m2ts`。
- `SourceIndex: string`：來源索引，供部分解析器保存上下文。
- `SourceType: string`：來源類型，例如 `MPLS`、`DVD`、`CUE`、`WebVTT` 等。
- `FramesPerSecond: decimal`：來源已知或自動推斷出的幀率。
- `Duration: TimeSpan`：片段總時長。
- `Chapters: List<Chapter>`：當前章節列表。
- `Expr: Expression`：時間轉換表達式，默認為 `Expression.Empty`，語義是 `t`。
- `TagType: Type`、`Tag: object`：解析器原始上下文；`Tag` 忽略 `null` 賦值。

核心行為：

- `CombineChapter(source, type)` 把多段章節合併為一段：每段章節時間加上前面片段累計 `Duration`，序號從 1 遞增，名稱生成 `Chapter NN`。
- `ChangeFps(fps)` 按舊幀率計算總幀數，再按新幀率重算章節時間和總時長；它會建立新的 `Chapter` 物件，只保留 `Name` 與新 `Time`，不保留原 `Number` 或 `FramesInfo`。
- `UpdateInfo(TimeSpan shift)` 讓所有章節時間減去 `shift`。
- `UpdateInfo(int shift)` 重排序號為 `++index + shift`。
- `UpdateInfo(string template)` 從模板文本逐行替換章節名；模板首尾空白、CR、LF 會先修剪，模板不足時保留原名並去掉尾部 `\r`。

### `ChapterInfoGroup`

`ChapterInfoGroup` 繼承 `List<ChapterInfo>`，子類只作來源類型標記：

- `BDMVGroup`
- `IfoGroup`
- `XplGroup`
- `MplsGroup`
- `XmlGroup`

Avalonia 版應保留「一組可選片段/Edition/Title」語義，但不應靠子類判斷 UI 行為；建議增加明確的 `ChapterSourceKind`。

### `ChapterName`

- 默認格式固定為 `Chapter NN`。
- `GetChapterName()` 返回閉包，每次調用產生下一個名稱。
- 實例 `Get()` 會返回當前 `Index` 並自增。
- `Range(start, count)` 只允許 `start` 在 `0..99` 且最大值不超過 99，否則拋出 `ArgumentOutOfRangeException`。

### `LanguageSelectionContainer`

- `Languages` 使用 ISO 639-2/B 代碼。
- `LanguagesTerminology` 使用 ISO 639-2/T 代碼，無 T 代碼時回退 B 代碼。
- `LookupISOCode` 支持 2 字母 ISO、3 字母 B/T 代碼反查語言名，找不到返回空字串。
- XML 導出 UI 從顯示文本中解析語言名，再用 `Languages[name]` 取得 3 字母 B 代碼。

### `DualDictionary<T1,T2>`

- 維護兩個字典實現雙向索引。
- `Bind` 會覆蓋兩個方向的映射；目前沒有移除、存在性檢查或衝突檢測。

## 4. 時間格式、幀率表、自動幀率、取整容差、K/* 標記

### 時間格式

- `Time2String(TimeSpan)` 輸出 `HH:mm:ss.sss`，毫秒由總秒小數部分用 `Math.Round` 預設 midpoint-to-even 規則取到 3 位；它不是 `MidpointRounding.AwayFromZero`。
- 若毫秒四捨五入為 `1000`，現有代碼只讓 `Seconds + 1` 並輸出 `.000`，不向分鐘/小時進位；因此邊界值可能輸出 `:60.000`。
- 小時使用 `TimeSpan.Hours` 而非總小時；核心重寫若要支持超過 24 小時，需要明確兼容策略。
- `RTimeFormat` 解析 `數字:數字:數字.三位毫秒` 或 `數字:數字:數字,三位毫秒`，分隔符周圍允許空白。
- `ToTimeSpan(string)` 對空白或不匹配格式返回 `TimeSpan.Zero`。
- `Chapter.Time2String(info)` 先以 `info.Expr.Eval(chapter.Time.TotalSeconds, info.FramesPerSecond)` 得到新秒數，再轉為 `TimeSpan` 和 `HH:mm:ss.sss`。
- `ToCueTimeStamp(TimeSpan)` 輸出 `mm:ss:ff`，分鐘為 `Hours * 60 + Minutes`，`ff = round(Milliseconds * 75 / 1000)`，最大封頂 99。

### 幀率表

`MplsData.FrameRate` 是現有核心幀率來源：

| 索引 | 值 |
| --- | --- |
| 0 | `0`，無效/自動檢測時跳過 |
| 1 | `24000 / 1001` |
| 2 | `24` |
| 3 | `25` |
| 4 | `30000 / 1001` |
| 5 | `0`，保留/無效，檢測時跳過 |
| 6 | `50` |
| 7 | `60000 / 1001` |

WinForms 下拉顯示 7 個項目：`24000 / 1001`、`24000 / 1000`、`25000 / 1000`、`30000 / 1001`、`RESER / VED`、`50000 / 1000`、`60000 / 1001`。現有 `Form1.cs` 對下拉索引和 `FrameRate` 索引的映射有特殊分支和疑似 off-by-one 風險；Avalonia/Core 必須改用顯式 `FrameRateOption { Code, DisplayName, Value, IsValid }`，不要保存控件索引。

### 自動幀率

啟用 `Round` 且沒有手動傳入幀率索引時，會自動檢測：

1. 對每個候選幀率計算所有章節的 `IsAccuracy(fps, tolerance, expr)` 總分。
2. 將索引 0 和 5 的結果置 0。
3. 取最大分數的第一個索引。
4. 設置 `_info.FramesPerSecond = MplsData.FrameRate[index]`。
5. 返回該索引；理論上若返回 0 則回退 1。

MPLS/DVD 來源使用來源已知幀率並禁用幀率下拉；其他來源允許選擇或自動檢測。

### 取整容差與 K/* 標記

容差菜單：

- `0.01`
- `0.05`
- `0.10`
- `0.15`，默認
- `0.20`
- `0.25`
- `0.30`

幀數公式：

```text
frames = CurrentInfo.Expr.Eval(chapter.Time.TotalSeconds, CurrentInfo.FramesPerSecond) * selectedFrameRate
```

`Round = true` 時：

- 顯示幀數為 `Math.Round(frames, MidpointRounding.AwayFromZero)`。
- 若 `abs(frames - rounded) < tolerance`，後綴為 ` K`。
- 否則後綴為 ` *`。

`Round = false` 時：

- 顯示未取整的 `decimal` 幀數，不加後綴。

QPF 導出會對 `FramesInfo.TrimEnd('K', '*') + "I"`，因此 `123 K` 和 `123 *` 都變成 `123 I`；未取整時可能變成 `123.456I`。

## 5. 表達式語法、變量、函數、錯誤行為

用途：把原始章節時間 `t` 轉為新時間。`Expression.Empty` 等價於變量 `t`。

支持兩種輸入：

- 中綴表達式：`new Expression(expr)`。
- 後綴表達式：`new Expression(expr.Split())`，讀到以 `//` 開頭的 token 即停止。

詞法與註釋：

- 空白會被忽略。
- `//` 後內容視為註釋。
- 數字支持小數點，以 `decimal.Parse` 解析。
- 變量名支持字母和 `_`，也允許後續數字。
- UI 文本校驗只允許數字、字母、`_`、空白、基本運算符、括號、逗號、點和 `//` 註釋；禁止 `1abc` 類變量；括號必須平衡。

變量：

- `t`：當前章節時間，單位秒。
- `fps`：當前幀率；當傳入 fps 小於 `1e-5` 時不提供該變量。

常量：

- `M_E`
- `M_LOG2E`
- `M_LOG10E`
- `M_LN2`
- `M_LN10`
- `M_PI`
- `M_PI_2`
- `M_PI_4`
- `M_1_PI`
- `M_2_PI`
- `M_2_SQRTPI`
- `M_SQRT2`
- `M_SQRT1_2`

函數：

- 一元：`abs`、`acos`、`asin`、`atan`、`cos`、`sin`、`tan`、`cosh`、`sinh`、`tanh`、`exp`、`log`、`log10`、`sqrt`、`ceil`、`floor`、`round`、`int`、`sign`
- 二元：`atan2`、`pow`、`max`、`min`
- 零元：`rand`
- 堆疊輔助：`dup`

中綴模式明確支持的運算符：

- 算術：`+`、`-`、`*`、`/`、`%`、`^`
- 比較：`>`、`<`、`>=`、`<=`

`OperatorTokens` 中列有 `and`、`or`、`xor`，求值器也有分支，但現有中綴優先級表沒有這三個 token；後綴構造器也會把多字符 token 當作變量。因此既有布爾操作屬殘留/不完整支持，不應在 Avalonia 重寫規格中承諾為可用功能，除非先補測並明確修正。

優先級：

- 比較為 `-1`
- `+`、`-` 為 `0`
- `*`、`/`、`%` 為 `1`
- `^` 為 `2`
- 一元負號通過補 `0` 處理。

錯誤行為：

- `ParseExpression` 捕獲構造異常，記錄 `Parse Failed`，回退 `Expression.Empty`，並顯示無效時間平移提示。
- `Eval(time, fps)` 捕獲求值異常後把該 `Expression` 標記為不可再求值，輸出控制台訊息，之後直接返回原始 `time`。
- `Eval()` 捕獲求值異常後標記不可再求值，之後返回 `0`。
- `Postfix2Infix` 在操作數不足或操作符不足時拋出異常。

需注意的既有疑點：

- `xor` 現有實現兩側都讀取第一個操作數，疑似 bug。
- 後綴構造器沒有把多字符 `and`、`or`、`xor` token 轉成 operator，需測試確認是否應兼容或修正。
- 三元 `?` 在求值器中有殘留分支，但 token 表未完整支持；不應作為 Avalonia 版已支持功能。

## 6. 章節編輯、刪除、插入、序號平移、章節名模板/自動命名

### 表格刷新

刷新流程：

1. 若需要更新幀數，先按來源類型更新 `FramesInfo`。
2. MPLS/DVD 使用來源幀率並禁用幀率選擇。
3. 其他來源使用當前幀率選項或自動檢測。
4. 行數變化或剛插入新行時重建表格；否則原地更新。

WinForms 行顯示：

- 序號列：`Number:D2`
- 時間列：`chapter.Time2String(info)`，會應用 `Expr`
- 章節名列：`AutoGenName ? ChapterName.Get(rowIndex + 1) : chapter.Name`
- 幀數列：`FramesInfo`

### 編輯

- 編輯時間列：使用 `TimeSpan.TryParse` 解析；大於 1 天則設為 `TimeSpan.Zero`，否則設為解析值。
- 編輯章節名列：直接設置 `Chapter.Name`。
- 編輯幀數列：從輸入中提取第一段數字，轉為 `int newFrame`；用當前幀率 `newFrame / fps` 換算成時間。
- 每次編輯後刷新幀數和行顯示。

### 刪除

刪除行時：

1. 從 `CurrentInfo.Chapters` 移除該行綁定的 `Chapter`。
2. 按 `OrderShift` 重排序號。
3. 如果刪除的是第一行，取新的第一章時間為 `newInitialTime`，所有章節時間減去它。
4. 若來源為 MPLS/IFO 且沒有章節名模板，刪除第一行後重新生成 `Chapter NN` 名稱。

### 插入

只允許選中單行時插入：

- 在選中行之前插入 `new Chapter("New Chapter", TimeSpan.Zero, 0)`。
- 立即按 `OrderShift` 重排序號。
- 標記新行已插入並刷新表格。

### 序號平移

`OrderShift` 來自數字控件，最大值 50。變更時執行 `UpdateInfo((int)value)`，所有章節序號重排為 `1 + shift` 開始。保存 TXT/OGM 時使用該序號。

### 章節名模板

啟用模板時打開文本文件，讀取時支持 UTF-8 BOM、UTF-16 LE、UTF-16 BE 和無 BOM UTF-8。模板逐行套用到章節名；模板不足時保留原名。取消模板時模板文本置空，但不回滾已寫入的章節名。

### 自動命名

`AutoGenName` 只影響顯示、預覽和導出：

- 顯示使用 `ChapterName.Get(rowIndex + 1)`。
- TXT/XML/CUE/JSON 導出使用新的 `ChapterName.GetChapterName()` 閉包從 `Chapter 01` 開始。
- 不修改 `Chapter.Name`。

### 幀平移

`Forward translation` 提示輸入向前平移的幀數：

1. 用當前幀率把幀數換算為 `shiftTime`。
2. 所有章節時間減去 `shiftTime`。
3. 刪除平移後小於 0 的章節。
4. 刷新表格。

### Create Zones

對選中行生成 `--zones` 參數：

- 每段起點為當前行 `FramesInfo` 中空格前的整數。
- 終點為下一行幀數減 1。
- 最後一行目前退化為用自身幀數計算，仍需測試確認是否應改用片段時長。

## 7. 七種導出格式的精確輸出契約與保存路徑規則

保存前會先刷新當前表格/幀數。保存類型由枚舉順序決定：

| 類型 | 後綴 |
| --- | --- |
| `TXT` | `.txt` |
| `XML` | `.xml` |
| `QPF` | `.qpf` |
| `TimeCodes` | `.TimeCodes.txt` |
| `TsmuxerMeta` | `.TsMuxeR_Meta.txt` |
| `CUE` | `.cue` |
| `JSON` | `.json` |

### 保存路徑

`GenerateSavePath(saveType)` 規則：

1. 若未設置自定義保存目錄，輸出到來源文件所在目錄；否則輸出到自定義保存目錄。
2. 基礎文件名為 `_bdmvTitle ?? Path.GetFileNameWithoutExtension(FilePath)`；只有 `_bdmvTitle` 為 `null` 時才回退，空字符串不會回退，可能生成以 `_{n}` 為主體的文件名。
3. 若來源擴展名是 `.mpls` 或 `.ifo`，追加 `__{CurrentInfo.SourceName}`。
4. 從 `_1` 開始追加遞增序號，直到 `{base}_{n}{suffix}` 不存在。
5. 右鍵保存按鈕可設置自定義保存目錄，並通過 `RegistryStorage.Save` 持久化到 `chaptertool.json`。

### 非 UI 主流程 helper

`ChapterInfo.GetCelltimes()` 輸出每個非分隔章節的幀號文本，按 `chapter.Time.TotalSeconds * FramesPerSecond` 四捨五入後逐行寫出，用於 x264 celltimes 類流程但未在主保存格式下拉中直接暴露。

`ChapterInfo.Chapter2Qpfile()` 生成 qpfile 文本，對每個非分隔章節輸出 `round(time * fps) I`。現有主 UI 保存流程沒有直接使用該 helper；Avalonia 重寫若要暴露，需新增顯式導出入口與測試。

### TXT / OGM

方法：`ChapterInfo.GetText(autoGenName)`

輸出每個 `Time != TimeSpan.MinValue` 的章節：

```text
CHAPTER{Number:D2}=hh:mm:ss.sss
CHAPTER{Number:D2}NAME={name}
```

時間會應用 `Expr`。名稱在 `autoGenName = true` 時使用 `Chapter 01` 起的自動名，否則使用 `Chapter.Name`。文本使用 `Environment.NewLine`，文件以 UTF-8 with BOM 寫出。

### XML / Matroska Chapters

方法：`ChapterInfo.SaveXml(filename, lang, autoGenName)`

輸出：

- XML writer 使用 UTF-8、縮進格式。
- 寫入 XML 聲明。
- 寫入一個註釋，內容為 `<!DOCTYPE Tags SYSTEM "matroskatags.dtd">`；不是正式 doctype。
- 根結構為 `Chapters/EditionEntry`。
- `EditionFlagHidden = 0`
- `EditionFlagDefault = 0`
- `EditionUID = random(1, int.MaxValue)`

每個 `Time != TimeSpan.MinValue` 的章節輸出：

```xml
<ChapterAtom>
  <ChapterDisplay>
    <ChapterString>name</ChapterString>
    <ChapterLanguage>lang</ChapterLanguage>
  </ChapterDisplay>
  <ChapterUID>random</ChapterUID>
  <ChapterTimeStart>hh:mm:ss.sss000</ChapterTimeStart>
  <ChapterFlagHidden>0</ChapterFlagHidden>
  <ChapterFlagEnabled>1</ChapterFlagEnabled>
</ChapterAtom>
```

`lang` 為空白時回退 `und`。時間會應用 `Expr`。名稱遵循 `autoGenName`。

### QPF

方法：`ChapterInfo.GetQpfile()`

每個 `Time != TimeSpan.MinValue` 的章節輸出一行：

```text
{FramesInfo.TrimEnd('K', '*')}I
```

典型取整輸出為 `123 I`。文件以 UTF-8 without BOM 寫出。

### TimeCodes

方法：`ChapterInfo.GetTimecodes()`

每個 `Time != TimeSpan.MinValue` 的章節輸出一行：

```text
hh:mm:ss.sss
```

時間會應用 `Expr`。文件以 UTF-8 with BOM 寫出。

### tsMuxeR Meta

方法：`ChapterInfo.GetTsmuxerMeta()`

輸出單段文本：

```text
--custom-
chapters=time1;time2;time3
```

時間會應用 `Expr`。末尾沒有分號。當章節列表為空時現有實現可能因 `Substring` 行為不符合預期，Core 應明確處理空列表。

### CUE

方法：`ChapterInfo.GetCue(sourceFileName, autoGenName)`

`sourceFileName` 由 `Path.GetFileName(FilePath)` 傳入，而不是 `SourceName`。

輸出：

```text
REM Generate By ChapterTool
TITLE "{CurrentInfo.Title}"
FILE "{sourceFileName}" WAVE
  TRACK 01 AUDIO
    TITLE "{name}"
    INDEX 01 mm:ss:ff
```

每個 `Time != TimeSpan.MinValue` 的章節生成一個 `TRACK NN AUDIO`，`NN` 從 1 遞增，不使用 `Chapter.Number`。CUE 時間使用原始 `chapter.Time.ToCueTimeStamp()`，不應用 `Expr`。文件以 UTF-8 with BOM 寫出。

### JSON

方法：`ChapterInfo.GetJson(autoGenName)`

Jil 序列化，字段：

```json
{
  "sourceName": "...",
  "chapter": [
    { "name": "...", "time": 0.0 }
  ]
}
```

規則：

- `sourceName`：`SourceType == "MPLS"` 時為 `{SourceName}.m2ts`，其他來源為 `null`。
- 普通章節輸出 `time = (chapter.Time - baseTime).TotalSeconds`。
- 遇到 `TimeSpan.MinValue` 且前一章存在時，將 `baseTime` 設為前一章時間，重置自動命名閉包，並額外輸出一個以 0 秒開始的章節，名稱取前一章名或新的自動名。
- JSON 不過濾所有 `TimeSpan.MinValue`；若分隔標記出現在列表第一項，現有行為可能輸出異常負時間，需測試固定。
- 文件以 UTF-8 with BOM 寫出。

## 8. Avalonia/Core 建議接口、ViewModel 命令/屬性

### Core 接口

建議拆分：

```csharp
public interface IChapterTimeFormatter
{
    string Format(TimeSpan time);
    TimeSpan ParseOrZero(string text);
    string FormatCue(TimeSpan time);
}

public interface IExpressionParser
{
    ExpressionParseResult Parse(string text, ExpressionMode mode);
}

public interface IFrameRateService
{
    IReadOnlyList<FrameRateOption> Options { get; }
    FrameInfoResult UpdateFrames(ChapterInfo info, FrameRateOption option, bool round, decimal tolerance);
    FrameRateOption Detect(ChapterInfo info, decimal tolerance);
}

public interface IChapterEditor
{
    void EditTime(ChapterInfo info, int index, TimeSpan time);
    void EditFrame(ChapterInfo info, int index, int frame, decimal fps);
    void EditName(ChapterInfo info, int index, string name);
    void Delete(ChapterInfo info, int index, int orderShift, bool regenerateNames);
    void InsertBefore(ChapterInfo info, int index, int orderShift);
    void ApplyOrderShift(ChapterInfo info, int shift);
    void ApplyNameTemplate(ChapterInfo info, string template);
}

public interface IChapterExporter
{
    ChapterExportKind Kind { get; }
    string Extension { get; }
    ChapterExportResult Export(ChapterInfo info, ChapterExportOptions options);
}

public interface ISavePathPolicy
{
    string Generate(string sourcePath, string customDirectory, string bdmvTitle, ChapterInfo info, ChapterExportKind kind);
}
```

`FrameRateOption` 應包含：

- `Code`：穩定代碼，例如 `Fps23976`、`Fps24`、`Fps25`。
- `DisplayName`
- `Value`
- `IsValid`
- `LegacyMplsIndex`，只供兼容測試使用。

### ViewModel 屬性

建議主 ViewModel 至少包含：

- `CurrentPath`
- `CurrentInfo`
- `InfoGroup`
- `SelectedClipIndex`
- `Chapters`
- `SelectedFrameRate`
- `FrameRateOptions`
- `RoundFrames`
- `FrameTolerance`
- `ApplyExpression`
- `ExpressionText`
- `ExpressionMode`
- `IsExpressionValid`
- `OrderShift`
- `AutoGenName`
- `UseChapterNameTemplate`
- `ChapterNameTemplate`
- `SelectedExportKind`
- `XmlLanguage`
- `LanguageOptions`
- `CustomSavingDirectory`
- `StatusText`
- `Progress`
- `CanSave`
- `CanEditChapters`

### ViewModel 命令

與本模塊直接相關：

- `RefreshCommand`
- `SaveCommand`
- `ChooseSaveDirectoryCommand`
- `SelectClipCommand`
- `SetFrameRateCommand`
- `SetToleranceCommand`
- `ToggleRoundCommand`
- `ToggleExpressionCommand`
- `SetExpressionCommand`
- `TogglePostfixExpressionCommand`
- `EditChapterTimeCommand`
- `EditChapterNameCommand`
- `EditChapterFrameCommand`
- `DeleteChapterCommand`
- `InsertChapterCommand`
- `ApplyOrderShiftCommand`
- `LoadChapterNameTemplateCommand`
- `ToggleAutoNameCommand`
- `ForwardTranslateCommand`
- `CreateZonesCommand`

命令結果應返回結構化訊息，例如 `Success`、`Warnings`、`StatusText`、`WrittenPath`，不要在核心服務中直接操作狀態欄或日誌窗口。

## 9. 未確定或需測試驗證的點

- `Time2String(TimeSpan)` 使用 `Hours` 而非總小時，且毫秒 1000 只進位到 seconds 欄位；超過 24 小時或秒進位到 60 的章節是否要保持舊行為需要決策。
- 非 MPLS/DVD 來源中 `_info.FramesPerSecond = MplsData.FrameRate[comboBox1.SelectedIndex]` 疑似與下拉索引不一致；Avalonia 版應以測試確認並消除索引依賴。
- 下拉項 `RESER / VED` 的現有非取整行為可能映射到無效幀率 0。
- `Expression` 的 `xor` 實現疑似錯誤。
- 後綴模式對多字符布爾運算符的支持需測試。
- 三元運算殘留代碼是否曾被外部使用未知。
- `ChapterInfo.GetCelltimes` / `Chapter2Qpfile` 未在主 UI 保存流程使用，是否納入 Core API 需確認。
- JSON 對 `TimeSpan.MinValue` 分隔符的處理需要針對首項分隔、連續分隔、尾部分隔補測。
- CUE 導出不應用 `Expr`，這可能是既有契約也可能是遺漏；重寫前需用用例固定。
- `Create Zones` 最後一行終點目前退化，應確認是否改用 `Duration`。
- XML 寫入的是 doctype 註釋而非正式 doctype，是否保持兼容需要決策。
- 空章節列表下 TXT/XML/QPF/TimeCodes/tsMuxeR/CUE/JSON 的行為需補測。

## 10. 本模塊覆蓋的源文件列表

覆蓋文件：

- `Time_Shift/Util/Chapter.cs`
- `Time_Shift/Util/ChapterInfo.cs`
- `Time_Shift/Util/ChapterInfoGroup.cs`
- `Time_Shift/Util/ChapterName.cs`
- `Time_Shift/Util/Expression.cs`
- `Time_Shift/Util/ToolKits.cs`（時間/編碼/保存語義；平台/配置主覆蓋見模塊 06）
- `Time_Shift/Util/LanguageSelectionContainer.cs`
- `Time_Shift/Util/DualDictionary.cs`
- `Time_Shift/Forms/Form1.cs`

輔助參照但不屬於本模塊主要覆蓋：

- `Time_Shift/Util/ChapterData/MplsData.cs`
- `Time_Shift/Forms/Form1.Designer.cs`
- `Time_Shift/Forms/Form1.resx`
- `Time_Shift/Properties/Resources.resx`
- `Time_Shift_Test/Util/ExpressionTests.cs`
- `Time_Shift_Test/Util/ToolKitsTests.cs`
