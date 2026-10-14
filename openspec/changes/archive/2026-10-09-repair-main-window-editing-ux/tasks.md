## 1. 建立回归证据

- [x] 1.1 重新执行 `openspec list --json`、`openspec status --change repair-main-window-editing-ux --json` 和 `openspec validate repair-main-window-editing-ux --strict`。复核既有历史及表达式 change 的兼容要求。
- [x] 1.2 在 `MainWindowHeadlessTests` 或同属 Headless 项目的专用回归类中加入中央表格区域和预览重叠的失败用例。使用生产资源、确定性章节和 `RunJobs`。
- [x] 1.3 建立空文档、多章节、长名称、长分支历史、`t + 1`、无效表达式及命名、编号偏移、帧率预览 fixture。记录修改前截图到 `artifacts/main-window-editing-ux/`。

## 2. 将历史移入弹框

- [x] 2.1 在 `AuxiliaryTools.cs`、窄会话端口和 `StandardToolCatalogFactory` 中加入历史工具。历史状态及导航必须复用当前 workspace。
- [x] 2.2 实现历史视图和 ViewModel。提供虚拟化列表、当前节点标识、分支关系、撤销、重做和会话生命周期提示。
- [x] 2.3 将主窗口历史 ToggleButton 替换为打开命令。移除历史侧栏、占位列和过时的展开状态。无会话时禁用入口。
- [x] 2.4 验证桌面模态宿主及已有 Embedded 宿主的次级展示。覆盖重复打开、关闭、Escape、后台交互隔离、焦点归还和资源释放。
- [x] 2.5 在 ViewModel 与 Headless 测试中验证弹框撤销、重做和分支导航。验证关闭重开后的当前节点及主表格结果。

## 3. 修正章节和选项布局

- [x] 3.1 将章节表格和空状态放入同一个可伸展工作区。移除中央双 star 行。保留表格列最小宽度、编辑和虚拟化。
- [x] 3.2 将选项表单和预览结果容器分离。调整 `ApplyAdvancedOptionsLayout`，确保所有宽窄状态具有有效的控件定位。
- [x] 3.3 为结果设置高度边界及独立滚动。将确认和取消操作放在结果滚动区之外。无结果时折叠区域。
- [x] 3.4 验证默认、宽和最小窗口下的末行滚动、单元格提交、空状态切换，以及宽→窄→宽之后的选择和草稿保留。

## 4. 修正内容预览展示

- [x] 4.1 用 typed candidate 和已有 `ExpressionPreviewProjector` 的业务格式替换主窗口技术字符串。按章节显示前值、后值和变化量。
- [x] 4.2 增加时间、帧信息和属性的分类计数。覆盖缺失值、不同帧率依据及小于一毫秒的变化。
- [x] 4.3 将命名、编号偏移和帧率候选接入同一可检查结果区域。显示操作名称和范围。状态条只保留简短状态。
- [x] 4.4 为紧凑结果提供完整计数、截取说明和完整差异入口。确保 1,000 个章节的全部差异可达。完整视图必须展示同一候选。
- [x] 4.5 验证有效、等待、无变化、错误、过期和失败状态。输入变化必须立即禁用旧候选。保留草稿及适用的更新入口。
- [x] 4.6 验证应用提交展示值且不重新计算、取消不提交、失败不部分提交及一次撤销恢复全部效果。布局切换不得改变候选身份。

## 5. 本地化和验收

- [x] 5.1 在共享 locale `.axaml` 中增加或复用历史及预览业务标签。验证中文和英文的长标签、按钮名称、单位和状态。保留既有语言切换能力。
- [x] 5.2 翻译资源变化后运行 `uv run --project scripts scripts/axaml-to-json.py`，再运行 `uv run --project scripts scripts/axaml-to-json.py --check`。仅在环境未安装时先执行 `uv sync --project scripts`。
- [x] 5.3 增加实际控件边界断言。覆盖 760×600、1280×800、760×520、860 和 861 宽度，以及连续尺寸切换。断言表格填充中央区、预览与表单不相交、操作可点击。
- [x] 5.4 验证代表性放大字体和浅色、深色主题。验证键盘导航、弹框关闭和焦点归还。关闭所有弹出控件并释放测试 DataContext。
- [x] 5.5 顺序运行 `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore` 和 `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore`。修复本 change 引起的失败。
- [x] 5.6 将中文和英文的主窗口、历史弹框及预览默认、宽、窄截图写入 `artifacts/main-window-editing-ux/`。复核列表空白、文本重叠、完整差异访问和操作可达性。
- [x] 5.7 更新 `docs/code-map/avalonia.md` 和 `docs/code-map/testing.md` 中变化的工具入口及测试归属。复核短句、术语及路径。
- [x] 5.8 再次运行 `openspec validate repair-main-window-editing-ux --strict`。报告测试结果与截图路径。若推送代码，先运行 `python scripts/check-ci.py`。
