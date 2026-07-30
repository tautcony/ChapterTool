## 1. Core: CLPI 二进制解析器

- [x] 1.1 创建 `ClpiParseLimits.cs`——CLPI 解析的有限边界常量与验证方法
- [x] 1.2 创建 `ClpiFile.cs`——顶层 CLPI 文件解析器，按 Section 地址跳转读取 ClipInfo / SequenceInfo / ProgramInfo / CPI
- [x] 1.3 创建 `ClpiClipInfo.cs`——ClipInfo 段（ClipStreamType, ApplicationType, IsCC5, TSRecordingRate, NumberOfSourcePackets）
- [x] 1.4 创建 `ClpiSequenceInfo.cs`——SequenceInfo 段（ATC 序列 + STC 序列，含 PresentationStartTime / PresentationEndTime）
- [x] 1.5 创建 `ClpiProgramInfo.cs`——ProgramInfo 段（Program 序列，含 StreamPID + StreamCodingInfo 引用）
- [x] 1.6 创建 `ClpiStreamCodingInfo.cs`——流编码信息（VideoFormat, FrameRate, VideoAspect, OCFlag, AudioFormat, SampleRate, LanguageCode 等）
- [x] 1.7 创建 `ClpiCPI.cs`——CPI/EPMap 段（Characteristic Point Information，含 EP coarse/fine 表的 PTS-SPN 映射）
- [x] 1.8 为 CLPI 解析器编写单元测试，覆盖有效 CLPI + 边界条件（零长度段、空 EP Map、IsCC5 标记）

## 2. Core: INDEX.BDMV 二进制解析器

- [x] 2.1 创建 `IndexParseLimits.cs`——INDEX 解析的有限边界常量
- [x] 2.2 创建 `IndexFile.cs`——顶层 index.bdmv 解析器，按 Section 地址跳转
- [x] 2.3 创建 `IndexAppInfoBDMV.cs`——AppInfoBDMV 段（InitialOutputModePreference, SSContentExistFlag, VideoFormat, FrameRate, UserData）
- [x] 2.4 创建 `IndexIndexes.cs`——Indexes 段（FirstPlaybackTitle, TopMenuTitle, NumberOfTitles + Title 列表）
- [x] 2.5 创建 `IndexTitleEntry.cs`——Title 条目（ObjectType, AccessType, PlaybackType, ObjectData）
- [x] 2.6 为 INDEX 解析器编写单元测试

## 3. Core: MPLS 章节导入器增强——CLPI 自动发现与 STC 感知

- [x] 3.1 新增 `BdmvPathHelper.cs`——BDMV 根目录推导与 CLPI 路径构造辅助方法
- [x] 3.2 在 `MplsChapterImporter` 中新增内部方法 `FindBdmvRoot`：从 `.mpls` 路径向上查找含 `BDMV/CLIPINF` 的父目录
- [x] 3.3 在 `MplsChapterImporter` 中新增内部方法 `DiscoverClpiFiles`：遍历 PlayItem 的 clip 名称，从 `BDMV/CLIPINF/{name}.clpi` 自动加载
- [x] 3.4 新增内部方法 `ResolveStcAwarePts`：利用已发现的 CLPI SequenceInfo 修正 PTS 偏移
- [x] 3.5 确保路径不在 BDMV 结构内时静默跳过 CLPI 发现（不产生 diagnostic 噪音）
- [x] 3.6 确保 CLPI 文件缺失或解析异常时回退到现有纯 MPLS 算法（记录 info/warning diagnostic）
- [x] 3.7 为路径推导 + CLPI 自动发现编写单元测试
- [x] 3.8 为 STC 感知计算编写单元测试（构造有非零 PresentationStartTime 的场景）

## 4. Infrastructure: 原生 BDMV 目录导入器

- [x] 4.1 创建 `NativeBdmvImporter.cs`——实现 `IChapterImporter`，ID 为 `"bdmv-native"`
- [x] 4.2 实现 BDMV 目录结构验证（BDMV/PLAYLIST 目录存在检查）
- [x] 4.3 实现 `index.bdmv` 发现与解析，提取 Movie 类型 Title → PlayList 映射
- [x] 4.4 实现 PlayList 候选发现：index 成功时用其指引，失败时降级为扫描 PLAYLIST/*.mpls
- [x] 4.5 实现 META/DL/*.xml 光盘标题读取
- [x] 4.6 对每个候选 PlayList 调用 `MplsChapterImporter.ImportAsync()`（CLPI 自动发现在其内部完成）
- [x] 4.7 聚合结果：每个候选 PlayList 产生一个 `ChapterImportEntry`，含光盘元数据和 CLPI 增强的章节时间
- [x] 4.8 实现进度报告（discovering titles → parsing playlists → building chapters）

## 5. Infrastructure: 注册原生导入器

- [x] 5.1 在 `RuntimeChapterImporterRegistry` 中注册 `NativeBdmvImporter`
- [x] 5.2 BDMV 目录路由指向原生导入器（替换当前直接路由到 eac3to 导入器的逻辑）
- [x] 5.3 eac3to 导入器保留作为可选增强路径（保留现有 BdmvChapterImporter 的完整代码和注册）

## 6. 测试与验证

- [x] 6.1 创建 CLPI 测试 Fixture 构建器（类似 `MplsBinaryBuilder` 的 `ClpiBinaryBuilder`）
- [x] 6.2 创建 INDEX 测试 Fixture 构建器
- [x] 6.3 添加集成测试：构造完整 BDMV 目录结构（index + mpls + clpi），验证端到端导入
- [x] 6.4 添加异常路径测试：缺失 CLPI、缺失 INDEX、损坏文件
- [ ] 6.5 用真实 BDMV 样本验证 MPLS+CLPI 组合解析的章节时间与 eac3to 输出一致（需 eac3to 环境）
