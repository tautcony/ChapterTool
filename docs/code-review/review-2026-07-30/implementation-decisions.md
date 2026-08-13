# libbluray 与 Core 差异实现决策

本文把 `libbluray` 与 `ChapterTool.Core` 的剩余差异拆成独立决策项。
请对每一项选择一个结果，再按选择创建或关闭实现任务。

## 目标边界

请选择本次工作的目标。

- [ ] **A. 章节导入等价**：只实现会改变标题发现、章节时间、章节数量或媒体引用的差异。
- [ ] **B. 完整 BDMV 元数据等价**：同时实现章节导入、导航元数据、UHD/3D/BDJO 元数据。
- [ ] **C. 播放器等价**：实现 libbluray 的播放状态机、M2TS、BD-J、图形交互和设备能力。

建议选择 **A**。选项 **C** 超出当前 Core 的职责边界，不适合作为 Core 的增量目标。

## P1 差异

### P1-1：MPLS 逐文件 BACKUP 回退

证据：`BdmvPlaylistScanner.cs` 先选择一个目录。libbluray 的 `mpls_get()` 会在每个 playlist 主路径失败后尝试 `BDMV/BACKUP/PLAYLIST`。

影响：主 `PLAYLIST` 目录存在但某个 `.mpls` 缺失或损坏时，Core 不会发现 BACKUP 中的有效 playlist。

请选择：

- [ ] **实现**：按文件名合并主目录和 BACKUP 目录。主文件解析失败时使用 BACKUP 文件。增加主缺失、主损坏、主有效三种测试。
- [x] **不实现**：只接受目录级回退。记录为已知兼容性差异。

推荐：**实现**。

### P1-2：CLPI 逐文件 BACKUP 回退

证据：`BdmvPathHelper.cs` 只构造 `BDMV/CLIPINF/{name}.clpi`。libbluray 的 `clpi_get()` 会逐文件尝试 BACKUP。

影响：主 CLPI 缺失或损坏时，Core 丢失该 clip 的 CLPI 元数据。

请选择：

- [ ] **实现**：增加主路径和 BACKUP 路径的逐文件解析及来源诊断。
- [ ] **不实现**：CLPI 只作为可选诊断信息，不保证 BACKUP 等价。

推荐：**实现**。

### P1-3：HDMV 全局标题编号映射

证据：`BdmvImporter.cs` 只把 HDMV 标题对象放入 `titleObjects`。BD-J 标题会从编号序列中被移除。libbluray 使用 INDEX 中的全局标题编号。

影响：当 HDMV 和 BD-J 标题交错时，`JUMP_TITLE` 或 `CALL_TITLE` 可能指向错误的 MovieObject。

请选择：

- [x] **实现**：保留全局标题表。HDMV、BD-J、First Playback 和 Top Menu 使用独立的强类型引用。增加交错标题跳转测试。
- [ ] **不实现**：只保证没有 BD-J 标题的 HDMV 光盘。遇到混合标题时依赖扫描回退。

推荐：**实现**。

## P2 差异

### P2-1：HDMV 导航指令覆盖范围

证据：Core 只处理部分 Branch、Compare、Set 和 `PlayPL*`。libbluray 还处理 `LinkPI`、`LinkMK`、`TerminatePL`、Still、Popup、Button、Stream、Output Mode、NV Timer 等操作。

影响：真实光盘可能通过未实现指令改变后续 playlist、play item、mark 或执行流程。当前实现可能静默忽略这些操作。

请选择：

- [x] **实现全部相关指令**：补齐会影响 playlist 发现的 Branch/System 指令，并对不影响章节的 UI 指令保留有界诊断。
- [ ] **实现最小章节子集**：只补齐 `LinkPI`、`LinkMK`、`TerminatePL` 和 `PlayStop`。其余 UI/播放指令明确诊断并回退扫描。
- [ ] **不实现**：接受当前 bounded subset，并把扫描回退作为正式策略。

推荐：**实现最小章节子集**。

### P2-2：INDEX 访问控制和隐藏标志

证据：Core 保存 `AccessType`，但 Native importer 未使用。libbluray 根据该字段设置 title accessible/hidden 状态。

影响：禁止访问或隐藏的 title 可能被导入，导致标题列表与 libbluray 不同。

请选择：

- [x] **实现**：跳过 prohibited title，并保留 hidden title 的诊断状态。
- [ ] **只诊断**：继续导入，但在结果中标记 prohibited/hidden。
- [ ] **不实现**：所有可解析 title 都参与章节导入。

推荐：**只诊断**。章节工具通常需要列出全部可解析内容，但用户必须能识别访问限制。

### P2-3：MPLS PiP `data_address` 基址

静态审查曾推测 `MplsExtensionData.cs` 与 libbluray 的 `data_address` 基址不同。
对真实 `00020_Terminator2.mpls` 的对拍没有复现该差异：29 条 PiP 记录和 98 条
PiP data 记录均一致。因此本项当前不是已确认缺陷。

影响：当前证据只要求保留回归对拍，不要求立即修改解析逻辑。

请选择：

- [x] **实现**：保留真实 PiP 对拍作为回归验证；只有新增 fixture 复现地址差异时才修改基址，并增加越界测试。
- [ ] **只保留原始数据**：停止解析 PiP 的嵌套记录，避免产生不可靠的 typed result。
- [ ] **不实现**：当前章节流程不使用 PiP 数据。

推荐：**实现**。这是已公开 typed API 的正确性问题。

### P2-4：CLPI 3D/SS 扩展

证据：libbluray 解析 extent start points、ProgramInfo SS 和 CPI SS。Core 只保存 CLPI extension entries 和 raw data block。

影响：3D/SSIF clip 的包定位和扩展诊断不完整。

请选择：

- [x] **实现**：增加 extent start points、ProgramInfo SS、CPI SS 的强类型记录和测试。
- [ ] **只保留原始数据**：把“不解析 3D/SS 扩展”写入 Core API 文档和诊断信息。
- [ ] **不实现**：当前项目不支持 3D/SSIF。

推荐：**只保留原始数据**，除非产品明确要求 3D 光盘支持。

### P2-5：INDEX UHD/HDR 扩展

证据：libbluray 解析 extension 3.1 中的 4K、HDR、HDR10+ 和 Dolby Vision 标志。Core 不解析 INDEX extension entries。

影响：UHD 光盘的显示能力诊断不完整。普通章节提取不受影响。

请选择：

- [x] **实现**：增加 extension entry 解析、UHD/HDR typed metadata 和版本覆盖测试。
- [ ] **不实现**：将其记录为非章节元数据差异。

推荐：**不实现**，除非 UI 或导出流程需要显示这些信息。

## 设计边界差异

### D-1：BDJO 完整结构

Core 只解析 accessible playlists，并且不执行 JAR/Xlet。libbluray 还解析 Terminal Info、Application Cache、Application Management、Key Interest 和 File Access。

请选择：

- [x] **实现完整 BDJO 元数据**：不执行 JAR/Xlet，但解析并暴露所有结构。
- [ ] **保持当前边界**：只解析 playlist 声明，动态 BD-J 使用扫描回退。

推荐：**保持当前边界**。

### D-2：CLPI 是否参与章节时间或包定位

当前 Core 会发现 CLPI，但 `MplsChapterImporter` 和 `MplsAggregateProjection` 仍使用 MPLS `INTime/OUTTime/MarkTimeStamp` 计算章节时间。

请选择：

- [ ] **保持当前行为**：CLPI 只提供诊断和可选元数据，不改变用户章节时间。
- [x] **扩展使用范围**：增加基于 STC/EP Map 的 packet lookup API，并单独定义用户时间线规则。

推荐：**保持当前行为**。不要再次把 `PresentationStartTime` 直接加到章节时间线上。

## 测试与验证

### V-1：Native libbluray 对拍

- [x] 安装 Homebrew `libbluray 1.5.0`。
- [x] 验证仓库 checkout 与 Homebrew 库都是 `1.5.0`。
- [x] 验证无需 Meson 的 `mpls_dump` 和 `clpi_dump` 编译命令。
- [x] 使用默认相关标题模式对比六张整盘的 title、playlist、duration、chapter。
- [x] 使用原生 API 对比 160 个 MPLS 文件的 marks、play item、stream、subpath 和 extension 数据。
- [x] 使用原生 API 对比 244 个 CLPI 文件的 sequence、program、CPI 和扩展计数。
- [ ] 对主路径损坏、BACKUP 回退、BD-J、HDMV 分支和更多扩展 fixture 执行对拍。

对拍发现：有效文件的字段和标题结果均一致。一个故意损坏的
`00001_Invalid.mpls` 被 Core 拒绝，但被 libbluray 接受为空 playlist。`bd_list_titles -a`
会显示 Core 按相关标题策略排除的无章节或重复片段候选；这是策略差异，不是解析差异。

完整命令和时间基准见 [`native-libbluray-parity.md`](./native-libbluray-parity.md)。
Homebrew 不安装 `mpls_dump` 或 `clpi_dump`，但可以直接编译仓库中的匹配版本并链接 Homebrew 动态库。

### V-2：现有测试状态

- [x] Core Importing 测试：253 通过。
- [ ] Infrastructure BDMV 测试：当前 25 通过、1 失败。

失败测试：

`BdmvImporterTests.NoChapterPlaylistIsRetainedAsDiagnosticAndNotImported`

该测试仍期待旧的逐条诊断消息。当前实现已经改为聚合扫描诊断。需要在继续实现前决定是否同步测试契约。

## 选择汇总

请在下表中填写最终选择。

| 项目 | 选择 |
|---|---|
| 目标边界 | 章节导入、导航与诊断元数据；不执行 BD-J 应用代码 |
| P1-1 MPLS BACKUP | 不实现。保留目录级回退策略 |
| P1-2 CLPI BACKUP | 未选择。本次不改变现有 CLPI 回退范围 |
| P1-3 HDMV 全局标题编号 | 实现。保留 INDEX 全局编号和独立 First Playback/Top Menu 引用 |
| P2-1 HDMV 指令 | 实现全部影响章节发现的指令；UI 指令保留有界诊断 |
| P2-2 INDEX 访问控制 | 跳过 prohibited title；保留 hidden 状态诊断 |
| P2-3 PiP 地址基址 | 保留真实 PiP 对拍和越界测试。不修改已验证的基址 |
| P2-4 CLPI 3D/SS | 实现 extent、ProgramInfo SS、CPI SS 强类型记录 |
| P2-5 INDEX UHD/HDR | 实现 extension 3.1 UHD/HDR typed metadata |
| D-1 BDJO 完整结构 | 实现元数据解析。不执行 JAR 或 Xlet |
| D-2 CLPI 时间线使用 | 增加 STC/EP Map 包定位 API。章节时间仍使用 MPLS 时间 |
| V-1 原生对拍 | 保留现有 160 MPLS、244 CLPI、六张光盘对拍结果 |
| V-2 测试契约同步 | 同步访问控制、扩展元数据、导航控制和包定位测试契约 |

## 对拍结果选择

以下项目表示证据状态，不替代上面的实现选择：

| 结果 | 状态 |
|---|---|
| MPLS BDMV 字段对拍 | [x] 160/160 一致 |
| CLPI BDMV 公共字段对拍 | [x] 244/244 一致 |
| 独立 MPLS 样本 | [x] 18/18 有效样本一致；1 个损坏样本严格性不同 |
| PiP 地址基址缺陷 | [ ] 未复现 |
| 相关标题集合、时长、章节数 | [x] 六张整盘一致 |
| BACKUP、HDMV 执行、BDJO、INDEX 访问控制 | [ ] 尚未覆盖 |
