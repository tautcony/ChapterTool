# 07 - 測試、構建、發佈與資產覆蓋

## 模塊目的

本模塊記錄舊工程中可作為 Avalonia 重寫驗收基線的測試、樣例、CI、安裝包與二進制/圖片資產。它不描述具體業務算法本身，而是回答兩個問題：

- 哪些現有功能已有可重用的測試或樣例。
- 哪些文件/資產在重寫時必須遷移、替代或明確棄用。

## 審閱過的文件

- 測試工程與配置
  - `Time_Shift_Test/Time_Shift_Test.csproj`
  - `Time_Shift_Test/app.config`
  - `Time_Shift_Test/Properties/AssemblyInfo.cs`
- 測試類
  - `Time_Shift_Test/Util/ExpressionTests.cs`
  - `Time_Shift_Test/Util/ToolKitsTests.cs`
  - `Time_Shift_Test/Util/OgmDataTests.cs`
  - `Time_Shift_Test/Util/VTTDataTests.cs`
  - `Time_Shift_Test/Util/CueDataTests.cs`
  - `Time_Shift_Test/Util/CueSheetTests.cs`
  - `Time_Shift_Test/Util/MplsDataTests.cs`
  - `Time_Shift_Test/Util/IfoDataTests.cs`
  - `Time_Shift_Test/Util/IfoParserTests.cs`
  - `Time_Shift_Test/Util/IfoTimeTests.cs`
  - `Time_Shift_Test/Knuckleball/MP4FileTests.cs`
  - `Time_Shift_Test/SharpDvdInfo/DvdInfoContainerTests.cs`
- 樣例資料
  - `Time_Shift_Test/[VTT_Sample]/chapter.vtt`
  - `Time_Shift_Test/[ogm_Sample]/00001.txt`
  - `Time_Shift_Test/[cue_Sample]/*.cue`
  - `Time_Shift_Test/[mpls_Sample]/*.mpls`
  - `Time_Shift_Test/[ifo_Sample]/VTS_05_0.IFO`
  - `Time_Shift_Test/[Video_Sample]/Chapter.mp4`
  - `Time_Shift_Test/Util/expression.in`
  - `Time_Shift_Test/Util/expression.out`
- 構建與發佈
  - `Time_Shift.sln`
  - `README.md`
  - `ChangeLog.md`
  - `LICENSE`
  - `.gitattributes`
  - `.gitignore`
  - `bump-version.pl`
  - `.github/workflows/dotnet-ci.yml`
  - `Time_Shift/Time_Shift.csproj`
  - `Time_Shift_Test/Time_Shift_Test.csproj`
  - `Time_Shift/Properties/AssemblyInfo.cs`
  - `Time_Shift_Test/Properties/AssemblyInfo.cs`
  - `Time_Shift/app.manifest`
  - `Time_Shift/App.config`
  - `Time_Shift/FodyWeavers.xml`
  - `Time_Shift/FodyWeavers.xsd`
- 安裝包
  - `dist/windows/chaptertool.nsi`
  - `dist/windows/options.nsi`
  - `dist/windows/installer.nsi`
  - `dist/windows/uninstaller.nsi`
  - `dist/windows/translations.nsi`
  - `dist/windows/installer-translations/english.nsi`
  - `dist/windows/installer-translations/simpchinese.nsi`
  - `dist/windows/UAC.nsh`
  - `dist/windows/nsis plugins/UAC.zip`
- 二進制與圖片資產
  - `Time_Shift/mp4v2/x64/libmp4v2.dll`
  - `Time_Shift/mp4v2/x86/libmp4v2.dll`
  - `Time_Shift/Images/*`
  - `Time_Shift/Resources/arrow_drop_down.bmp`

## 測試工程結構

舊測試工程是 MSTest v1 風格 `.NET Framework 4.8` 測試項目，引用主工程 `Time_Shift/Time_Shift.csproj`，並使用 `FluentAssertions 6.12.0`。測試主要集中在解析器和工具函數，不覆蓋 WinForms UI 交互。

重寫後建議：

- 將解析器與核心邏輯測試遷移到 `ChapterTool.Core.Tests`。
- 將外部工具與文件系統相關測試拆到 `ChapterTool.Infrastructure.Tests`，使用可替換的 process/file service。
- 新增 `ChapterTool.Avalonia.Tests` 或 ViewModel 級測試，用於覆蓋舊 WinForms 事件邏輯。

## 測試類覆蓋功能

### ExpressionTests

覆蓋：

- 中綴表達式轉 postfix。
- postfix 輸入與 `Postfix2Infix`。
- 加減乘除、取模、冪運算。
- 空表達式行為。
- 函數：`floor`、`ceil`、`log10`、`abs`、三角函數。
- `expression.in` / `expression.out` 大量算例。

缺口：

- `t`、`fps` 變量在章節時間轉換中的集成測試不足。
- 布爾運算、`and/or/xor`、比較運算沒有充分測試。
- UI 表達式校驗正則沒有測試。

### ToolKitsTests

覆蓋：

- `TimeSpan` 與 `hh:mm:ss.sss` 互轉。
- 幀率值到 `MplsData.FrameRate` 索引的映射。
- 特定時間格式與舍入細節。

缺口：

- UTF BOM 文本讀取、CUE 時間戳、顏色配置、配置文件讀寫未覆蓋。

### OgmDataTests

覆蓋：

- OGM/TXT 樣例解析。
- 章節名與時間戳輸出。
- 初始時間歸零的解析結果。

缺口：

- 解析中斷後返回部分結果的容錯路徑未明確覆蓋。
- 空行、非法首行、錯誤 NAME 行缺少測試。

### VTTDataTests

覆蓋：

- WebVTT 樣例解析 smoke test；目前只調用 parser 並輸出結果，沒有章節數、章節名或時間戳斷言。

缺口：

- 無 `WEBVTT` 頭、cue 中缺少 `-->`、多行 cue 文本、NOTE/STYLE 等 WebVTT 節點未覆蓋。

### CueDataTests / CueSheetTests

覆蓋：

- CUE 樣例解析；目前自動斷言主要使用 `ARCHIVES 2.cue`。
- 章節數、章節名、時間戳。
- `CueSharp` CUE sheet 模型的基礎讀取 smoke test；目前只輸出 track，沒有斷言。

缺口：

- FLAC/TAK 內嵌 CUE 提取未有端到端測試。
- `example-cue-sheet-1.cue` 和日文檔名樣例被保留在測試資產中，但未納入自動斷言。
- 多 FILE、多 INDEX、非標準編碼、REM/PERFORMER 等字段覆蓋不足。

### MplsDataTests

覆蓋：

- 多個 `.mpls` 樣例。
- PlayItem 數量、ClipName、IN/OUT time、幀率字段。
- Mark timestamp 與 PlayItem 關聯。
- 多 clip full name 組合，例如 `00006&00007`。

缺口：

- `GetChapters()` 轉 `ChapterInfo` 的完整輸出驗收不足。
- Form1 中片段選擇、合併、追加 MPLS、打開對應 m2ts 未測試。
- 無章節/無效 MPLS/多 angle 邊界需要補測。

### IfoDataTests / IfoParserTests / IfoTimeTests

覆蓋：

- IFO 樣例解析為章節集合。
- 章節數、章節名、時間。
- BCD 轉換。
- IFO time/frame 計算與 NTSC/PAL 相關計算。

缺口：

- 多 PGC、多 title、無章節、錯誤 IFO 結構未充分覆蓋。
- Form1 中 IFO 合併章節與對應 VOB 打開菜單未測試。

### MP4FileTests

覆蓋：

- MP4 樣例章節解析。
- 章節數、章節名、時間。

缺口：

- `libmp4v2.dll` 缺失行為未覆蓋。
- x86/x64 DLL 複製策略與硬鏈接讀取策略未覆蓋。

### DvdInfoContainerTests

覆蓋：

- SharpDvdInfo 對 DVD 信息容器的章節解析。
- bit extraction 方法正確性。

缺口：

- DVD 音頻、字幕、視頻 metadata 到 UI/章節功能的實際使用較少，重寫時需判斷是否保留完整模型。

## 樣例文件用途

- `[VTT_Sample]/chapter.vtt`：WebVTT importer 驗收。
- `[ogm_Sample]/00001.txt`：OGM/TXT importer 驗收。
- `[cue_Sample]/*.cue`：CUE importer 資產；目前自動測試只使用 `ARCHIVES 2.cue`，其他 CUE 樣例是保留資產，重寫測試應補上日文檔名與多樣例驗收。
- `[mpls_Sample]/*.mpls`：Blu-ray playlist parser 驗收，覆蓋單 clip、多 clip、mark 分佈。
- `[ifo_Sample]/VTS_05_0.IFO`：DVD IFO parser 驗收。
- `[Video_Sample]/Chapter.mp4`：MP4 chapter parser 驗收。
- `expression.in/out`：表達式求值批量算例。

Avalonia 重寫時應把樣例資料保留到測試資產中，並避免測試依賴當前工作目錄的脆弱相對路徑。建議使用測試項目中的 `CopyToOutputDirectory` 或嵌入資源策略。

## CI 與構建

根目錄工程與元資料：

- `Time_Shift.sln` 包含兩個 C# project：主程序 `Time_Shift/Time_Shift.csproj` 與測試項目 `Time_Shift_Test/Time_Shift_Test.csproj`。solution 配置覆蓋 `Debug/Release` 與 `Any CPU/x64/x86`。
- `README.md` 是公開功能摘要與依賴說明來源，但比現有代碼少列了 `TimeCodes`、`TsMuxeR Meta`、`CUE` 等保存格式；重寫後 README 需以本規格更新。
- `ChangeLog.md` 包含歷史功能/bugfix，但歷史格式不規整、部分記錄不完整；只能作輔助線索，不應作唯一需求來源。
- `LICENSE` 是 GPLv3+ 授權，Avalonia 重寫和第三方依賴替換需保持授權兼容。
- `.gitattributes`、`.gitignore` 控制源碼、生成物和二進制文件管理；新工程需重新檢查 obj/bin、publish、native DLL、測試樣例是否被正確跟蹤或忽略。
- `bump-version.pl` 是舊 release 腳本，使用 git-flow，修改 `AssemblyInfo.cs` 中 `AssemblyVersion`/`AssemblyFileVersion`，提交、打 tag 並 finish release。它不更新 `dist/windows/options.nsi` 的 `PROG_VERSION`；當前 assembly 版本與 installer 版本來源分離且數值不一致，重寫後應統一版本來源。Avalonia SDK-style 項目若改用 csproj/Directory.Build.props 管版本，需替換這個流程。

`.github/workflows/dotnet-ci.yml`：

- 在 `windows-2022` runner 上構建。
- 安裝 .NET 5 SDK，但實際使用 MSBuild/nuget restore 構建 `.NET Framework 4.8` solution。
- matrix 僅包含 `Release` + `x64`。
- 上傳 `Time_Shift/bin/x64/Release/`，排除 XML 和 PDB。

Avalonia 重寫建議：

- 改為 `dotnet restore/build/test/publish`。
- matrix 至少覆蓋 `windows-latest`；若目標跨平台，補 `ubuntu-latest`、`macos-latest`。
- 明確區分 framework-dependent 和 self-contained 發佈。
- 增加測試步驟，目前 CI 只構建未執行測試。

## 工程文件與依賴

主工程：

- 目標框架：`.NET Framework 4.8`。
- 輸出類型：WinExe。
- Root namespace/Assembly：`ChapterTool`。
- Debug/Release 支持 AnyCPU/x86/x64。
- Release AnyCPU `Prefer32Bit=false`，Debug AnyCPU `Prefer32Bit=true`。
- `TreatWarningsAsErrors=true`。
- 應用圖標：`Images/icon.ico`。
- manifest：`app.manifest`。
- PackageReference：
  - `Costura.Fody`
  - `Fody`
  - `Jil`
  - `Sigil`
  - `StyleCop.Analyzers`
- 引用大量 WinForms/System.Drawing/.NET Framework assembly。
- `mp4v2/x64/libmp4v2.dll` 和 `mp4v2/x86/libmp4v2.dll` 根據平台複製為輸出目錄 `libmp4v2.dll`。

測試工程：

- 目標框架：`.NET Framework 4.8`。
- MSTest 舊式項目。
- 引用主工程。
- PackageReference：`FluentAssertions`。

Avalonia 重寫建議：

- 新建 SDK-style solution。
- 核心庫使用 `net8.0` 或更新 LTS，UI 使用 Avalonia 支持的 TFM。
- 用 `System.Text.Json` 或明確替代 `Jil`，除非需要保持 JSON 行為兼容。
- 移除 Costura/Fody，改用標準 publish 或單文件發佈策略。
- `System.Drawing` 和 WinForms 相關引用只留在兼容/遷移層，不能進入 Core。

## 安裝包與發佈資產

NSIS 腳本：

- `chaptertool.nsi` 只是聚合入口，include options/translations/installer/uninstaller。
- `options.nsi` 定義：
  - Unicode installer。
  - DPI aware manifest。
  - LZMA solid compression。
  - 版本 `2.33.33.331`。
  - 輸出 `ChapterTool_${PROG_VERSION}_setup.exe`。
  - 安裝目錄根據 32/64 位選擇 Program Files。
  - 使用 UAC macro 嘗試提權。
  - 安裝頁面包含 welcome/license/components/directory/install/finish。
- `installer.nsi`：
  - 安裝 `ChapterTool.exe`、config、`libmp4v2.dll`、英文 resources dll。
  - 寫入 `HKLM\Software\ChapterTool\InstallLocation`。
  - 寫入 Windows uninstall registry。
  - 可選桌面快捷方式。
  - 建立開始菜單快捷方式。
  - 安裝完成可啟動 ChapterTool。
- `uninstaller.nsi`：
  - 刪除 exe/config/libmp4v2/en-US resources/license/uninstaller。
  - 刪除快捷方式。
  - 刪除 uninstall registry、`Software\ChapterTool`、`Software\Classes\ChapterTool`。
  - 通知 shell association changed。

Avalonia 重寫建議：

- 若繼續使用 NSIS，需更新輸出文件、resources、runtime 文件、版本來源。
- 現有 NSIS `PROG_VERSION` 不由 `bump-version.pl` 更新；新發佈流程應由單一版本源生成 assembly、installer、README/release notes。
- 若改用 MSIX/Squirrel/Velopack/自包含 zip，需重定義文件關聯、卸載、快捷方式與 UAC 策略。
- 安裝包目前假定 Windows，跨平台包需要單獨設計。

## 二進制與圖片資產

- `mp4v2/x64/libmp4v2.dll`、`mp4v2/x86/libmp4v2.dll`
  - MP4 importer 必需。
  - 舊 csproj 按平台複製為 `libmp4v2.dll`。
  - Avalonia 重寫需決定是否保留 native mp4v2、改用純 .NET MP4 parser，或將 MP4 支持設為可選 plugin。

- `Time_Shift/SharpDvdInfo/LICENSE`
  - SharpDvdInfo 第三方授權文件；若保留或替換 DVD reader，需重新核對授權、notice 與發佈包包含策略。

- `Images/icon.ico`、`Images/about.ico`
  - 主程序和 About/icon 使用。

- `Images/arrow_drop_down.png`、`Images/arrow_drop_up.png`、`Images/unfold_more.png`、`Resources/arrow_drop_down.bmp`
  - 舊 UI 展開/折疊與下拉圖標。
  - Avalonia 可改用向量圖標或 Avalonia 資源，但需要保持展開/收起狀態語義。

## 現有測試缺口

重寫前/重寫中應補：

- 主窗口 ViewModel 命令測試：載入、切換 clip、合併、追加、刷新、保存、預覽、日誌、配色。
- 導出快照測試：TXT、XML、QPF、TimeCodes、TsMuxeR Meta、CUE、JSON。
- 保存路徑避重名與自定義保存目錄測試。
- 表格編輯測試：時間編輯、幀號編輯、章節名編輯、刪除第一行、插入行。
- 表達式集成測試：`t`/`fps` 對顯示與導出都生效。
- 外部依賴缺失測試：mkvextract、eac3to、libmp4v2。
- 配置遷移測試：舊 `chaptertool.json` 鍵名兼容。
- 語言資源切換測試。
- BDMV 目錄載入測試可用 mock process，不應依賴本機 eac3to。

## 本模塊覆蓋的源文件/資產列表

- `.github/workflows/dotnet-ci.yml`
- `dist/windows/chaptertool.nsi`
- `dist/windows/installer.nsi`
- `dist/windows/installer-translations/english.nsi`
- `dist/windows/installer-translations/simpchinese.nsi`
- `dist/windows/nsis plugins/UAC.zip`
- `dist/windows/options.nsi`
- `dist/windows/translations.nsi`
- `dist/windows/UAC.nsh`
- `dist/windows/uninstaller.nsi`
- `Time_Shift/Time_Shift.csproj`
- `Time_Shift_Test/Time_Shift_Test.csproj`
- `Time_Shift_Test/app.config`
- `Time_Shift_Test/Properties/AssemblyInfo.cs`
- `Time_Shift/Properties/AssemblyInfo.cs`
- `Time_Shift/app.manifest`
- `Time_Shift/App.config`
- `Time_Shift/FodyWeavers.xml`
- `Time_Shift/FodyWeavers.xsd`
- `Time_Shift/mp4v2/x64/libmp4v2.dll`
- `Time_Shift/mp4v2/x86/libmp4v2.dll`
- `Time_Shift/Images/about.ico`
- `Time_Shift/Images/arrow_drop_down.png`
- `Time_Shift/Images/arrow_drop_up.png`
- `Time_Shift/Images/icon.ico`
- `Time_Shift/Images/unfold_more.png`
- `Time_Shift/Resources/arrow_drop_down.bmp`
- `Time_Shift/SharpDvdInfo/LICENSE`
- `Time_Shift_Test/[cue_Sample]/ARCHIVES 2.cue`
- `Time_Shift_Test/[cue_Sample]/example-cue-sheet-1.cue`
- `Time_Shift_Test/[cue_Sample]/のんのんびより りぴーと オリジナルサウンドトラック.cue`
- `Time_Shift_Test/[ifo_Sample]/VTS_05_0.IFO`
- `Time_Shift_Test/[mpls_Sample]/00001_fch.mpls`
- `Time_Shift_Test/[mpls_Sample]/00002_tanji.mpls`
- `Time_Shift_Test/[mpls_Sample]/00011_eva.mpls`
- `Time_Shift_Test/[ogm_Sample]/00001.txt`
- `Time_Shift_Test/[Video_Sample]/Chapter.mp4`
- `Time_Shift_Test/[VTT_Sample]/chapter.vtt`
- `Time_Shift.sln`
- `README.md`
- `ChangeLog.md`
- `LICENSE`
- `.gitattributes`
- `.gitignore`
- `bump-version.pl`
- `Time_Shift_Test/Knuckleball/MP4FileTests.cs`
- `Time_Shift_Test/SharpDvdInfo/DvdInfoContainerTests.cs`
- `Time_Shift_Test/Util/CueDataTests.cs`
- `Time_Shift_Test/Util/CueSheetTests.cs`
- `Time_Shift_Test/Util/expression.in`
- `Time_Shift_Test/Util/expression.out`
- `Time_Shift_Test/Util/ExpressionTests.cs`
- `Time_Shift_Test/Util/IfoDataTests.cs`
- `Time_Shift_Test/Util/IfoParserTests.cs`
- `Time_Shift_Test/Util/IfoTimeTests.cs`
- `Time_Shift_Test/Util/MplsDataTests.cs`
- `Time_Shift_Test/Util/OgmDataTests.cs`
- `Time_Shift_Test/Util/ToolKitsTests.cs`
- `Time_Shift_Test/Util/VTTDataTests.cs`
