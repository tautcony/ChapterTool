# ChapterTool 重寫文檔覆蓋矩陣

本矩陣用於校驗 `Time_Shift`、`Time_Shift_Test`、`dist`、`.github` 下的代碼、測試、配置與資產是否均被重寫文檔覆蓋。`主要模塊` 表示負責完整說明該文件功能/遷移含義的子文檔；`參照模塊` 表示該文件也被其他模塊作為上下文引用。

## 模塊索引

| 模塊 | 文檔 | 責任範圍 |
| --- | --- | --- |
| 01 | `docs/modules/01-ui-shell-and-interactions.md` | 主窗口 UI、命令、快捷鍵、拖放、表格交互 |
| 02 | `docs/modules/02-core-model-transform-export.md` | 核心模型、時間/幀率/表達式、章節編輯、導出 |
| 03 | `docs/modules/03-text-xml-matroska-vtt-importers.md` | OGM/TXT、XML、Matroska、WebVTT |
| 04 | `docs/modules/04-disc-playlist-media-importers.md` | MPLS、IFO/DVD、XPL、BDMV、MP4 |
| 05 | `docs/modules/05-cue-flac-tak-importers.md` | CUE、FLAC、TAK、CueSharp |
| 06 | `docs/modules/06-supporting-ui-platform-services.md` | 輔助窗口、控件、平台服務、配置、資源 |
| 07 | `docs/modules/07-tests-build-distribution-assets.md` | 測試、樣例、CI、安裝包、工程/資產 |

## 生產代碼與資源

| 文件 | 主要模塊 | 參照模塊 | 覆蓋說明 |
| --- | --- | --- | --- |
| `Time_Shift/Program.cs` | 01 | 06,07 | 啟動參數、runtime 檢查、主窗體入口 |
| `Time_Shift/Forms/Form1.cs` | 01 | 02,03,04,05,06 | 主窗口事件與跨模塊編排 |
| `Time_Shift/Forms/Form1.Designer.cs` | 01 | 02,06 | 主窗口控件、菜單、事件綁定 |
| `Time_Shift/Forms/Form1.resx` | 01 | 06 | 中文/默認 UI 資源與控件元數據 |
| `Time_Shift/Forms/Form1.en-US.resx` | 01 | 06 | 英文 UI 資源 |
| `Time_Shift/Util/Chapter.cs` | 02 | 01,07 | 單章節模型、幀精度判定 |
| `Time_Shift/Util/ChapterInfo.cs` | 02 | 01,03,04,05,07 | 章節集合模型、導出、合併 |
| `Time_Shift/Util/ChapterInfoGroup.cs` | 02 | 04 | 多片段/Edition group 類型 |
| `Time_Shift/Util/ChapterName.cs` | 02 | 01 | 自動章節名 |
| `Time_Shift/Util/Expression.cs` | 02 | 07 | 時間表達式解析/求值 |
| `Time_Shift/Util/ToolKits.cs` | 06 | 02,03,05,07 | 配置/平台 helper；時間、編碼與保存語義由 02/03/05 引用 |
| `Time_Shift/Util/LanguageSelectionContainer.cs` | 02 | 06 | XML 語言下拉與 ISO 代碼 |
| `Time_Shift/Util/DualDictionary.cs` | 02 | 06 | 雙向字典 helper |
| `Time_Shift/Util/ChapterData/OgmData.cs` | 03 | 01,07 | OGM/TXT importer |
| `Time_Shift/Util/ChapterData/VTTData.cs` | 03 | 07 | WebVTT importer |
| `Time_Shift/Util/ChapterData/XmlData.cs` | 03 | 02,07 | Matroska chapter XML importer |
| `Time_Shift/Util/ChapterData/MatroskaData.cs` | 03 | 06,07 | mkvextract 調用與 XML 取得 |
| `Time_Shift/Util/ChapterData/Serializable/MatroskaChapters.cs` | 03 | 07 | XML 序列化模型 |
| `Time_Shift/Util/ChapterData/StreamUtils.cs` | 04 | 03 | 二進制 stream/bit helper |
| `Time_Shift/Util/ChapterData/MplsData.cs` | 04 | 02,07 | Blu-ray MPLS parser、幀率表 |
| `Time_Shift/Util/ChapterData/IfoData.cs` | 04 | 07 | DVD IFO parser |
| `Time_Shift/Util/ChapterData/IfoParser.cs` | 04 | 07 | IFO BCD/time helper |
| `Time_Shift/Util/ChapterData/XplData.cs` | 04 | 07 | HD-DVD XPL parser |
| `Time_Shift/Util/ChapterData/BDMVData.cs` | 04 | 03,06,07 | BDMV/eac3to importer |
| `Time_Shift/Util/ChapterData/Mp4Data.cs` | 04 | 07 | MP4 chapter adapter |
| `Time_Shift/Knuckleball/Chapter.cs` | 04 | 07 | MP4 chapter native wrapper model |
| `Time_Shift/Knuckleball/IntPtrExtensions.cs` | 04 | 07 | native pointer helper |
| `Time_Shift/Knuckleball/MP4File.cs` | 04 | 07 | libmp4v2 MP4 reader |
| `Time_Shift/Knuckleball/NativeMethods.cs` | 04 | 06,07 | MP4 P/Invoke |
| `Time_Shift/SharpDvdInfo/DvdInfoContainer.cs` | 04 | 07 | DVD information reader |
| `Time_Shift/SharpDvdInfo/DvdTypes/DvdAudio.cs` | 04 | 07 | DVD audio metadata |
| `Time_Shift/SharpDvdInfo/DvdTypes/DvdSubpicture.cs` | 04 | 07 | DVD subtitle metadata |
| `Time_Shift/SharpDvdInfo/DvdTypes/DvdVideo.cs` | 04 | 07 | DVD video metadata |
| `Time_Shift/SharpDvdInfo/Model/AudioProperties.cs` | 04 | 07 | DVD audio model |
| `Time_Shift/SharpDvdInfo/Model/SubpictureProperties.cs` | 04 | 07 | DVD subtitle model |
| `Time_Shift/SharpDvdInfo/Model/TitleInfo.cs` | 04 | 07 | DVD title model |
| `Time_Shift/SharpDvdInfo/Model/VideoProperties.cs` | 04 | 07 | DVD video model |
| `Time_Shift/SharpDvdInfo/Model/VmgmInfo.cs` | 04 | 07 | DVD VMGM model |
| `Time_Shift/SharpDvdInfo/LICENSE` | 07 | 04 | 第三方組件授權 |
| `Time_Shift/Util/ChapterData/CueData.cs` | 05 | 02,07 | CUE/FLAC/TAK importer |
| `Time_Shift/Util/ChapterData/FlacData.cs` | 05 | 07 | FLAC metadata/內嵌 CUE reader |
| `Time_Shift/Util/CueSharp.cs` | 05 | 07 | CueSharp CUE model/parser |
| `Time_Shift/ChapterData/IData.cs` | 05 | 02 | importer 資料接口 |
| `Time_Shift/Forms/FormPreview.cs` | 06 | 01 | 預覽窗口 |
| `Time_Shift/Forms/FormPreview.Designer.cs` | 06 | 01 | 預覽窗口控件 |
| `Time_Shift/Forms/FormPreview.resx` | 06 | 07 | 預覽窗口資源 |
| `Time_Shift/Forms/FormLog.cs` | 06 | 01 | 日誌窗口 |
| `Time_Shift/Forms/FormLog.Designer.cs` | 06 | 01 | 日誌窗口控件 |
| `Time_Shift/Forms/FormLog.resx` | 06 | 07 | 日誌窗口資源 |
| `Time_Shift/Forms/FormColor.cs` | 06 | 01 | 配色窗口 |
| `Time_Shift/Forms/FormColor.Designer.cs` | 06 | 01 | 配色窗口控件 |
| `Time_Shift/Forms/FormColor.resx` | 06 | 07 | 配色窗口資源 |
| `Time_Shift/Forms/FormAbout.cs` | 06 | 07 | About/托盤行為 |
| `Time_Shift/Forms/FormAbout.Designer.cs` | 06 | 07 | About 控件 |
| `Time_Shift/Forms/FormAbout.resx` | 06 | 07 | About 資源 |
| `Time_Shift/Forms/FormUpdater.cs` | 06 | 07 | 更新窗口 |
| `Time_Shift/Forms/FormUpdater.Designer.cs` | 06 | 07 | 更新窗口控件 |
| `Time_Shift/Forms/FormUpdater.resx` | 06 | 07 | 更新窗口資源 |
| `Time_Shift/Controls/cTextBox.cs` | 06 | 01 | 預覽/文本控件 |
| `Time_Shift/Controls/cTextBox.resx` | 06 | 07 | 控件資源 |
| `Time_Shift/Controls/HiLightTextBox.cs` | 06 | 01 | 高亮文本控件 |
| `Time_Shift/Util/Logger.cs` | 06 | 01,07 | 全局日誌 |
| `Time_Shift/Util/Notification.cs` | 06 | 01 | 消息框/輸入框 |
| `Time_Shift/Util/NativeMethods.cs` | 06 | 04,07 | Windows API、硬鏈接、DPI、shell |
| `Time_Shift/Util/SystemMenu.cs` | 06 | 01 | 系統菜單 |
| `Time_Shift/Util/TaskAsync.cs` | 06 | 04 | 外部進程 async helper |
| `Time_Shift/Util/Updater.cs` | 06 | 07 | 更新檢查/下載 |
| `Time_Shift/Util/LanguageHelper.cs` | 06 | 01 | WinForms 語言資源應用 |
| `Time_Shift/Properties/Resources.Designer.cs` | 06 | 07 | 強類型資源生成物 |
| `Time_Shift/Properties/Resources.resx` | 06 | 01,07 | 默認資源 |
| `Time_Shift/Properties/Resources.en-US.resx` | 06 | 01,07 | 英文資源 |
| `Time_Shift/Properties/Settings.Designer.cs` | 06 | 07 | 設定生成物 |
| `Time_Shift/Properties/Settings.settings` | 06 | 07 | 設定定義 |
| `Time_Shift/Properties/AssemblyInfo.cs` | 07 | 06 | 程序元數據/版本 |
| `Time_Shift/App.config` | 07 | 06 | .NET Framework runtime/config |
| `Time_Shift/app.manifest` | 07 | 06 | Windows manifest |
| `Time_Shift/FodyWeavers.xml` | 07 | 06 | Fody 配置 |
| `Time_Shift/FodyWeavers.xsd` | 07 | 06 | Fody schema |
| `Time_Shift/Time_Shift.csproj` | 07 | 06 | 主工程構建/依賴/資產 |
| `Time_Shift/Images/about.ico` | 07 | 06 | About/圖標資產 |
| `Time_Shift/Images/arrow_drop_down.png` | 07 | 06 | 展開/下拉資產 |
| `Time_Shift/Images/arrow_drop_up.png` | 07 | 06 | 展開/收起資產 |
| `Time_Shift/Images/icon.ico` | 07 | 06 | 應用圖標 |
| `Time_Shift/Images/unfold_more.png` | 07 | 06 | 展開/收起資產 |
| `Time_Shift/Resources/arrow_drop_down.bmp` | 07 | 06 | 舊 bitmap 資產 |
| `Time_Shift/mp4v2/x64/libmp4v2.dll` | 07 | 04 | MP4 native x64 DLL |
| `Time_Shift/mp4v2/x86/libmp4v2.dll` | 07 | 04 | MP4 native x86 DLL |

## 測試與樣例

| 文件 | 主要模塊 | 參照模塊 | 覆蓋說明 |
| --- | --- | --- | --- |
| `Time_Shift_Test/Time_Shift_Test.csproj` | 07 | 02,03,04,05 | 測試工程構建與依賴 |
| `Time_Shift_Test/app.config` | 07 | 06 | 測試 runtime config |
| `Time_Shift_Test/Properties/AssemblyInfo.cs` | 07 | 06 | 測試程序集元數據 |
| `Time_Shift_Test/Util/ExpressionTests.cs` | 07 | 02 | 表達式測試 |
| `Time_Shift_Test/Util/ToolKitsTests.cs` | 07 | 02 | 時間/幀率 helper 測試 |
| `Time_Shift_Test/Util/OgmDataTests.cs` | 07 | 03 | OGM importer 測試 |
| `Time_Shift_Test/Util/VTTDataTests.cs` | 07 | 03 | WebVTT importer 測試 |
| `Time_Shift_Test/Util/CueDataTests.cs` | 07 | 05 | CUE importer 測試 |
| `Time_Shift_Test/Util/CueSheetTests.cs` | 07 | 05 | CueSharp parser 測試 |
| `Time_Shift_Test/Util/MplsDataTests.cs` | 07 | 04 | MPLS parser 測試 |
| `Time_Shift_Test/Util/IfoDataTests.cs` | 07 | 04 | IFO importer 測試 |
| `Time_Shift_Test/Util/IfoParserTests.cs` | 07 | 04 | IFO helper 測試 |
| `Time_Shift_Test/Util/IfoTimeTests.cs` | 07 | 04 | IFO time/frame 測試 |
| `Time_Shift_Test/Knuckleball/MP4FileTests.cs` | 07 | 04 | MP4 importer 測試 |
| `Time_Shift_Test/SharpDvdInfo/DvdInfoContainerTests.cs` | 07 | 04 | SharpDvdInfo 測試 |
| `Time_Shift_Test/Util/expression.in` | 07 | 02 | 表達式批量輸入 |
| `Time_Shift_Test/Util/expression.out` | 07 | 02 | 表達式批量期望輸出 |
| `Time_Shift_Test/[ogm_Sample]/00001.txt` | 07 | 03 | OGM 樣例 |
| `Time_Shift_Test/[VTT_Sample]/chapter.vtt` | 07 | 03 | WebVTT 樣例 |
| `Time_Shift_Test/[cue_Sample]/ARCHIVES 2.cue` | 07 | 05 | CUE 樣例 |
| `Time_Shift_Test/[cue_Sample]/example-cue-sheet-1.cue` | 07 | 05 | CUE 樣例 |
| `Time_Shift_Test/[cue_Sample]/のんのんびより りぴーと オリジナルサウンドトラック.cue` | 07 | 05 | CUE 日文樣例 |
| `Time_Shift_Test/[mpls_Sample]/00001_fch.mpls` | 07 | 04 | MPLS 樣例 |
| `Time_Shift_Test/[mpls_Sample]/00002_tanji.mpls` | 07 | 04 | MPLS 多 clip 樣例 |
| `Time_Shift_Test/[mpls_Sample]/00011_eva.mpls` | 07 | 04 | MPLS 樣例 |
| `Time_Shift_Test/[ifo_Sample]/VTS_05_0.IFO` | 07 | 04 | IFO 樣例 |
| `Time_Shift_Test/[Video_Sample]/Chapter.mp4` | 07 | 04 | MP4 樣例 |

## CI 與安裝包

| 文件 | 主要模塊 | 參照模塊 | 覆蓋說明 |
| --- | --- | --- | --- |
| `.github/workflows/dotnet-ci.yml` | 07 | 06 | GitHub Actions build |
| `dist/windows/chaptertool.nsi` | 07 | 06 | NSIS 聚合入口 |
| `dist/windows/options.nsi` | 07 | 06 | NSIS 全局選項、版本、UAC |
| `dist/windows/installer.nsi` | 07 | 06 | 安裝步驟、registry、快捷方式 |
| `dist/windows/uninstaller.nsi` | 07 | 06 | 卸載步驟 |
| `dist/windows/translations.nsi` | 07 | 06 | 安裝器翻譯聚合 |
| `dist/windows/installer-translations/english.nsi` | 07 | 06 | 英文安裝器翻譯 |
| `dist/windows/installer-translations/simpchinese.nsi` | 07 | 06 | 簡中安裝器翻譯 |
| `dist/windows/UAC.nsh` | 07 | 06 | NSIS UAC macro |
| `dist/windows/nsis plugins/UAC.zip` | 07 | 06 | NSIS UAC plugin |

## 根目錄工程與倉庫文件

| 文件 | 主要模塊 | 參照模塊 | 覆蓋說明 |
| --- | --- | --- | --- |
| `Time_Shift.sln` | 07 | 01,02,03,04,05,06 | solution 拓撲與配置 |
| `README.md` | 07 | 01,02,03,04,05 | 公開功能/依賴摘要 |
| `ChangeLog.md` | 07 | 01,02,03,04,05,06 | 歷史功能與 bugfix 線索 |
| `LICENSE` | 07 | 04,05,06 | GPLv3+ 授權 |
| `.gitattributes` | 07 | 06 | 倉庫屬性與文本/二進制處理 |
| `.gitignore` | 07 | 06 | 忽略規則與生成物管理 |
| `bump-version.pl` | 07 | 06 | 舊 release/version 腳本 |

## 覆蓋校驗狀態

截至本矩陣生成時，`rg --files Time_Shift Time_Shift_Test dist .github` 與根目錄工程/倉庫文件均已在上方表格中分配主要模塊。第二輪驗證需要逐文檔確認：

- 文檔是否真的說明了矩陣聲明的主要文件。
- 是否存在文件雖被列出，但功能細節不足以支持 Avalonia 重寫。
- 是否存在文件雖被分配主要模塊，但只列名而未描述其重寫影響。
