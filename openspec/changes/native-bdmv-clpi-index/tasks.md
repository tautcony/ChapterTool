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
- [x] 4.3 Replace the invalid INDEX-to-MPLS mapping with typed HDMV and BD-J references.
- [x] 4.4 Discover playlist candidates through navigation evidence and bounded playlist scanning.
- [x] 4.5 实现 META/DL/*.xml 光盘标题读取
- [x] 4.6 Add and use an aggregate MPLS projection. Do not delegate BDMV titles to standalone PlayItem import.
- [x] 4.7 Create one `ChapterImportEntry` for each complete chapter-bearing playlist.
- [x] 4.8 实现进度报告（discovering titles → parsing playlists → building chapters）

## 5. Infrastructure: 注册原生导入器

- [x] 5.1 在 `RuntimeChapterImporterRegistry` 中注册 `NativeBdmvImporter`
- [x] 5.2 Route disc-root, `BDMV` directory, and primary `index.bdmv` inputs to the corrected native importer.
- [x] 5.3 eac3to 导入器保留作为可选增强路径（保留现有 BdmvChapterImporter 的完整代码和注册）

## 6. 测试与验证

- [x] 6.1 创建 CLPI 测试 Fixture 构建器（类似 `MplsBinaryBuilder` 的 `ClpiBinaryBuilder`）
- [x] 6.2 创建 INDEX 测试 Fixture 构建器
- [x] 6.3 添加集成测试：构造完整 BDMV 目录结构（index + mpls + clpi），验证端到端导入
- [x] 6.4 添加异常路径测试：缺失 CLPI、缺失 INDEX、损坏文件
- [x] 6.5 用真实 BDMV 样本验证 MPLS+CLPI 组合解析的章节时间与 eac3to 输出一致（需 eac3to 环境）

## 7. Core: MovieObject Parsing

- [x] 7.1 Add bounded MovieObject file and section limits.
- [x] 7.2 Add typed MovieObject, object, instruction, command, and operand models.
- [x] 7.3 Parse the common BDMV header and the MovieObject section at byte 40.
- [x] 7.4 Decode every 12-byte command field and preserve both 32-bit operands.
- [x] 7.5 Implement primary and `BDMV/BACKUP` MovieObject selection.
- [x] 7.6 Add synthetic parser tests for every instruction field and malformed boundary.
- [x] 7.7 Add parser tests that use the repository MovieObject fixtures.

## 8. Core: Bounded HDMV Navigation Resolver

- [x] 8.1 Define deterministic PSR defaults and GPR initialization.
- [x] 8.2 Implement immediate, GPR, and PSR operand reads. Reject normal PSR writes.
- [x] 8.3 Implement required Branch, Compare, Set, and SetSystem operations.
- [x] 8.4 Emit typed `PlayPL`, `PlayPLPI`, and `PlayPLPM` events.
- [x] 8.5 Implement object jump, object call, title jump, title call, resume, and call-stack behavior.
- [x] 8.6 Add instruction, transition, call-depth, event, profile, and visited-state limits.
- [x] 8.7 Add deterministic random behavior or bounded deterministic outcome forks.
- [ ] 8.8 Add optional bounded player-profile variants only for PSRs that the program reads.
- [x] 8.9 Add unit tests for instruction semantics, register-based playlist selection, branches, calls, cycles, and every limit.

## 9. Core: BDJO Parsing

- [x] 9.1 Add typed BD-J INDEX references and BDJO models.
- [x] 9.2 Parse the accessible-playlist count, access-to-all flag, autostart flag, and playlist names.
- [x] 9.3 Implement primary and `BDMV/BACKUP/BDJO` selection.
- [x] 9.4 Add tests for explicit lists, access-to-all, autostart, truncation, invalid names, and limits.
- [x] 9.5 Add the unsupported dynamic BD-J diagnostic. Do not execute JAR files or Xlets.

## 10. Infrastructure: Discovery and Layout

- [x] 10.1 Add `BdmvSourceLayout` for disc-root, `BDMV` directory, and primary `index.bdmv` input.
- [x] 10.2 Reject arbitrary `.bdmv` files as top-level input.
- [x] 10.3 Add a bounded MPLS scanner with structural duplicate and repeated-segment filtering.
- [x] 10.4 Keep navigation evidence separate from scan evidence.
- [x] 10.5 Add one deterministic parity discovery policy that merges and deduplicates evidence.
- [x] 10.6 Retain no-chapter candidates for parity diagnostics and omit them from chapter entries.
- [x] 10.7 Preserve first-use clip order, remove duplicate clips, and include angle clips.
- [x] 10.8 Emit source, fallback, profile, and unsupported-navigation diagnostics.

## 11. eac3to Parity and Regression Tests

- [x] 11.1 Commit eac3to reference manifests for every BDMV fixture.
- [x] 11.2 Add exact tests for title identity, order, duration, chapter count, clip collection, and chapter timestamps.
- [x] 11.3 Add exact input-equivalence tests for the three accepted input forms.
- [x] 11.4 Add a live opt-in parity check for `C:\Tools\eac3to\eac3to.exe`.
- [x] 11.5 Verify the full STEINS;GATE disc values in `eac3to-alignment-plan.md`.
- [x] 11.6 Verify that standard tests do not require eac3to or the external full disc.
- [x] 11.7 Run focused Core, Infrastructure, and Avalonia tests in sequence.
- [ ] 11.8 Run `dotnet test ChapterTool.slnx --no-restore`.
- [x] 11.9 Update `docs/code-map/core.md` and `docs/code-map/infrastructure.md` after implementation.
