## Why

当前 Core 项目仅实现了 MPLS 文件的二进制解析，缺失 CLPI 和 INDEX.BDMV 的原生解析能力。这导致：
1. 多 STC 序列场景下需要 CLPI SequenceInfo 和 EPMap 元数据以支持入口包定位，同时章节时间必须保持 MPLS 时间基准
2. 无法自动从 BDMV 目录中识别主影片 PlayList（缺少 INDEX.BDMV 的 Title 结构）
3. 独立使用 Core 库时必须依赖 eac3to 外部工具才能完成 BDMV 目录导入

此次变更补齐 Core 对完整 BD-ROM 文件格式的解析能力，使 MPLS 章节导入不再依赖外部工具。

## What Changes

- **Core 层新增**：CLPI 文件二进制解析器（ClipInfo → SequenceInfo → ProgramInfo → CPI → StreamCodingInfo）
- **Core 层新增**：INDEX.BDMV 文件二进制解析器（AppInfoBDMV → Indexes → TitleEntry）
- **Core 层增强**：`MplsChapterImporter` 自动发现 CLPI，并保留 STC/EPMap 元数据供包定位；章节时间仍使用 MPLS 时间基准
- **Infrastructure 层新增**：`NativeBdmvImporter`——纯 C# 原生 BDMV 目录导入器，无需 eac3to
- **Infrastructure 层调整**：BDMV 目录路由优先使用原生导入器，eac3to 不可用时自动降级

## Capabilities

### New Capabilities

- `bdmv-native-clpi-parsing`：Core 原生 CLPI 文件解析
- `bdmv-native-index-parsing`：Core 原生 INDEX.BDMV 文件解析
- `bdmv-native-directory-import`：Infrastructure 原生 BDMV 目录导入

### Modified Capabilities

- `disc-playlist-media-importers`：新增 CLPI/INDEX 解析和原生 BDMV 导入的需求规格

## Impact

- 影响代码：Core 新增 `Importing/Disc/Clpi*.cs`、`Importing/Disc/Index*.cs`；Infrastructure 新增 `Importing/Bdmv/NativeBdmvImporter.cs`
- 无新外部依赖，纯托管代码
- 向后兼容：现有 MPLS 导入和行为不变；CLPI 缺失时不中断解析
- eac3to 路径保留不变，原生路径作为独立新增
