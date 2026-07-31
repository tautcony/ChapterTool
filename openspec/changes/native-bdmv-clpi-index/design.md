## Context

## Alignment Revision

This revision is authoritative when later text conflicts with it.

The native importer must parse `MovieObject.bdmv`. It must execute a bounded subset of HDMV navigation instructions that can produce playlist events. The native importer must also parse BDJO accessible-playlist declarations.

The importer must not execute BD-J JAR files or Xlets. It must diagnose dynamic BD-J playlist selection and use bounded playlist-scan evidence.

`index.bdmv` does not map an HDMV title directly to an MPLS file. It maps an HDMV title to a MovieObject identifier. The MovieObject program can select a playlist through immediate or register operands.

The authoritative implementation plan is `eac3to-alignment-plan.md`. That plan defines parser limits, resolver behavior, libbluray references, eac3to manifests, tests, and acceptance criteria.

当前 ChapterTool 的 BD-ROM 解析能力仅限于 MPLS 文件。完整的 BD-ROM 应用格式包含三种核心文件类型：

```
BDMV/
├── index.bdmv          ← Title table with HDMV or BD-J object references
├── MovieObject.bdmv    ← HDMV navigation objects (required)
├── PLAYLIST/
│   └── *.mpls          ← 已实现
├── CLIPINF/
│   └── *.clpi          ← 本次新增
├── STREAM/
│   └── *.m2ts          ← 传输流（无需解析）
└── META/DL/
    └── *.xml           ← 光盘元数据（已支持读取标题）
```

This design adds CLPI, INDEX, MovieObject, and BDJO parsing. It adds a native BDMV import path that does not require eac3to.

## Goals / Non-Goals

**Goals:**
- Core 层实现 CLPI 文件（`.clpi`）的完整二进制解析，包含 ClipInfo、SequenceInfo、ProgramInfo、CPI、StreamCodingInfo
- Core 层实现 INDEX.BDMV 文件（`index.bdmv`）的完整二进制解析，包含 AppInfoBDMV、Indexes、TitleEntry
- Core must parse `MovieObject.bdmv` and resolve bounded HDMV navigation.
- Core must parse BDJO accessible-playlist declarations.
- CLPI 数据作为 MPLS 章节导入的**可选增强**——提供时用于 STC、入口包和流元数据，缺失时不中断解析
- Infrastructure must merge HDMV events, BDJO declarations, and playlist-scan evidence.
- 原生 BDMV 导入器与现有 eac3to 路径并存，通过 import request 区分或 fallback

**Non-Goals:**
- The change will not implement HDMV menu rendering or interactive graphics.
- 不实现 M2TS 传输流解析
- 不替换或移除现有 eac3to 路径
- The change will not execute BD-J JAR files or Xlets.
- 不解析 ClipMark（CLPI 内的标记段，极少用于章节）

## Decisions

### 1. CLPI 解析器放在 Core 的 `Importing/Disc/` 命名空间下

**Rationale:** CLPI 与 MPLS 同属 BD-ROM 碟片二进制格式，共享 `BinaryReadExtensions`、`MplsBoundedStream` 等基础设施。放在同一命名空间保持内聚性。

**文件结构：**
```
src/ChapterTool.Core/Importing/Disc/
├── Clpi/                          ← 新增 CLPI 子目录
│   ├── ClpiFile.cs                ← 顶层 CLPI 文件（类似 MplsPlaylistFile）
│   ├── ClpiClipInfo.cs            ← ClipInfo 段
│   ├── ClpiSequenceInfo.cs        ← SequenceInfo 段
│   ├── ClpiProgramInfo.cs         ← ProgramInfo 段
│   ├── ClpiCPI.cs                 ← CPI / EPMap 段
│   ├── ClpiStreamCodingInfo.cs    ← StreamCodingInfo（更详细的流编码信息）
│   └── ClpiParseLimits.cs         ← CLPI 专用 parse limits
├── Index/                         ← 新增 INDEX 子目录
│   ├── IndexFile.cs               ← 顶层 index.bdmv 文件
│   ├── IndexAppInfoBDMV.cs        ← AppInfoBDMV 段
│   ├── IndexIndexes.cs            ← Indexes 段（含 Title 列表）
│   └── IndexTitleEntry.cs         ← 单个 Title 条目
└── ... (existing MPLS files)
```

### 2. CLPI 数据通过 BDMV 目录结构自动发现，无需用户干预

**Rationale:** 用户加载 `.mpls` 时不应手动指定 CLPI 文件——交互体验太差。应利用 BDMV 的固定目录布局自动发现：给定 MPLS 文件路径，向上推导 BDMV 根目录，再向下定位 `CLIPINF/{clipName}.clpi`。

**自动发现路径推导：**

```
用户加载: /disc/BDMV/PLAYLIST/00001.mpls
                     ↑
推导 BDMV 根 = /disc
                          ↓
CLPI 查找:  /disc/BDMV/CLIPINF/00001.clpi
            /disc/BDMV/CLIPINF/00002.clpi  (多 clip 时分别查找)
```

**两层自动发现机制：**

```
MplsChapterImporter.ImportAsync(request)
│
├── 1. 解析 MPLS → 得到所有 PlayItem 的 ClipInformationFileName
│         例如: ["00001", "00002"]
│
├── 2. 从 request.Path 推导 BDMV 根目录
│         Path: ".../BDMV/PLAYLIST/00001.mpls"
│         向上查找含 "BDMV" 的父目录 → BDMV 根
│
├── 3. 自动发现 CLPI（best-effort，失败不中断）
│         foreach clipName:
│             clpiPath = "{bdmvRoot}/BDMV/CLIPINF/{clipName}.clpi"
│             if File.Exists → 解析并提取 SequenceInfo
│             else → 跳过（该 clip 无 STC 增强）
│
├── 4. 使用可用 CLPI 数据计算章节时间
│         有 CLPI → 保留 STC/EPMap 元数据供包定位
│         无 CLPI → 现有纯 MPLS 算法
│
└── 5. 返回结果（含 CLPI 可用性 diagnostics）
```

**API 设计：**

```csharp
// 内部方法：从 MPLS 路径推导 BDMV 根并发现 CLPI
internal static IReadOnlyDictionary<string, ClpiFile>? DiscoverClpiFiles(
    string mplsPath,
    IReadOnlyList<string> clipNames);

// 内部方法：STC 感知的 PTS 计算
private static ulong ResolveStcAwarePts(
    MplsPlayItem playItem,
    IReadOnlyDictionary<string, ClpiFile>? clpiMap);
```

**回退策略：**
- MPLS 路径不在 BDMV 目录结构内（如用户直接拖入单个 `.mpls`）→ 跳过 CLPI 发现，纯 MPLS 工作
- BDMV 结构内但 CLPI 文件缺失 → 跳过该 clip 的 CLPI，记录 info diagnostic
- CLPI 文件解析异常 → 跳过该 clip 的 CLPI，记录 warning diagnostic
- **任何时候 CLPI 缺失都不会导致章节导入失败**

### 3. INDEX.BDMV 解析器返回 Title 列表，由上层决定使用哪个

**Rationale:** INDEX.BDMV does not contain chapter data. An HDMV title references a MovieObject identifier. A BD-J title references a BDJO name. The parser must preserve these references as different typed values.

```csharp
// IndexFile.Read() 返回可用的 Title 列表和全局元数据
public sealed record IndexFile(
    string TypeIndicator,
    string VersionNumber,
    IndexAppInfoBDMV AppInfoBDMV,
    IndexIndexes Indexes);

public sealed record IndexIndexes(
    uint Length,
    IndexTitleEntry FirstPlaybackTitle,
    IndexTitleEntry TopMenuTitle,
    IReadOnlyList<IndexTitleEntry> Titles);

public sealed record IndexTitleEntry(
    byte ObjectType,       // 1=MovieObject, 2=BD-J Object
    byte AccessType,       // Movie / Interactive
    ushort PlaybackType,   // 0=Movie, 1=Interactive, 2=BD-J Movie, 3=BD-J Interactive
    IndexObjectReference ObjectReference);
```

### 4. 原生 BDMV 导入器放在 Infrastructure 层

**Rationale:** BDMV import requires file-system discovery. Infrastructure owns source-layout normalization, navigation-file selection, playlist scanning, and evidence merging. Core owns binary parsing, bounded navigation resolution, and aggregate MPLS projection.

**导入流程：**

```
NativeBdmvImporter.ImportAsync(bdmvRootPath)
│
├── 1. 验证 BDMV 目录结构（BDMV/PLAYLIST 存在）
│
├── 2. Parse index.bdmv.
│   ├── HDMV reference → execute the referenced MovieObject with limits.
│   ├── BD-J reference → parse the referenced BDJO playlist declaration.
│   └── Missing navigation data → retain a diagnostic and scan MPLS files.
│
├── 3. Merge navigation events with bounded playlist-scan evidence.
│
├── 4. Project each candidate as one aggregate MPLS playlist.
│   ├── Keep complete-playlist chapter marks and duration.
│   ├── Keep ordered and distinct clip references.
│   └── Omit no-chapter candidates from ChapterImportEntry output.
│
├── 5. Read META/DL/*.xml for the disc title when available.
│
└── 6. Return ChapterImportResult and discovery diagnostics.
```

### 5. BDMV 路由与 CLPI 发现策略

**两种加载路径，统一 CLPI 自动发现：**

```
路径 A: 用户加载 BDMV 目录（如 /disc/）
┌─────────────────────────────────────────────────────┐
│ RuntimeChapterImporterRegistry.Resolve("/disc/")    │
│   → 检测到 BDMV/PLAYLIST 子目录                     │
│   → 路由到 NativeBdmvImporter                       │
│     → resolve MovieObject or BDJO navigation         │
│     → merge bounded playlist-scan evidence           │
│     → build one aggregate entry per playlist         │
└─────────────────────────────────────────────────────┘

路径 B: 用户直接加载单个 .mpls（如 /disc/BDMV/PLAYLIST/00001.mpls）
┌─────────────────────────────────────────────────────┐
│ RuntimeChapterImporterRegistry.Resolve("....mpls")  │
│   → 扩展名 ".mpls" → 路由到 MplsChapterImporter     │
│     → 从路径向上推导 BDMV 根                        │
│     → 自动发现 CLPI → STC 增强                      │
│     → 若不在 BDMV 结构内 → 纯 MPLS 工作             │
└─────────────────────────────────────────────────────┘
```

**关键原则：** CLPI 发现逻辑集中在 `MplsChapterImporter` 一处，无论是通过 BDMV 目录导入还是单文件导入，都能自动受益。用户无需任何额外操作。

## Risks / Trade-offs

| 风险 | 缓解措施 |
|------|---------|
| CLPI EPMap PTS 拼接逻辑复杂（coarse + fine bits） | 使用 BoundedStream + 位操作，参考 BluRay 库已验证的算法 |
| An INDEX entry does not name an MPLS file | Preserve a typed HDMV or BD-J reference. Resolve it through MovieObject or BDJO data. |
| An HDMV program can loop or branch by player state | Apply instruction, state, transition, event, profile, and call-depth limits. |
| A BD-J Xlet can select a playlist dynamically | Do not execute the Xlet. Emit a diagnostic and use bounded scan evidence. |
| 原生解析的章节时间可能与 eac3to 有微小差异 | 使用与 eac3to 相同的 PTS/45000 转换，差异应在亚毫秒级 |
| 大型 CLPI 的 EPMap 可能包含大量 Entry Point | 用 `MplsBoundedStream` 约束读取边界，限制最大条目数 |
| 原生 BDMV 路径章节名称不如 eac3to 导出的丰富 | 章节命名回退到 `Chapter 01` 等默认名称，保持可用性 |
| MPLS 文件不在标准 BDMV 目录结构内 | 路径推导失败时静默跳过 CLPI 发现，纯 MPLS 工作，无任何 diagnostic 噪音 |

## Key Design Patterns

### BDMV 根目录推导

```csharp
// 从 MPLS 文件路径向上查找 BDMV 根目录
internal static string? FindBdmvRoot(string mplsPath)
{
    // 例如: /disc/BDMV/PLAYLIST/00001.mpls
    // → 查找路径片段中含 "BDMV" 的父目录
    // → 返回 /disc
    var dir = Path.GetDirectoryName(mplsPath);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir, "BDMV", "CLIPINF")))
            return dir;
        var parent = Path.GetDirectoryName(dir);
        if (parent == dir) break;
        dir = parent;
    }
    return null; // 不在 BDMV 结构内
}
```

### CLPI 作为 MPLS 的增强附件（自动发现，非手动传入）

```
MplsPlayItem
├── ClipName.ClipInformationFileName = "00001"
├── RefToSTCID = 0
├── INTime = 450000
└── OUTTime = 45045000

对应 CLPI:
├── ClpiClipInfo.NumberOfSourcePackets      // 用于验证时长
└── ClpiSequenceInfo.STCSequences[RefToSTCID]
    ├── PresentationStartTime = 0           // STC 基准 PTS
    ├── PresentationEndTime = 45225000
    ├── SPNSTCStart                         // 源包号起始
    └── PCRPID                              // PCR 的 PID
```

`PresentationStartTime` 和 `SPNSTCStart` 只用于 STC/入口包定位。Playlist 章节时间仍由 MPLS 的 `INTime`、`OUTTime` 和 mark 时间计算。

### Parse Limits 模式（延续 MPLS 的习惯）

```csharp
internal static class ClpiParseLimits
{
    internal const int MaximumClipInfoLength = 64 * 1024;
    internal const int MaximumSequenceInfoLength = 256 * 1024;
    internal const int MaximumProgramInfoLength = 256 * 1024;
    internal const int MaximumCPILength = 4 * 1024 * 1024;
    internal const int MaximumStreamPIDEntries = 256;
    internal const int MaximumEPCoarseEntries = 65536;
    internal const int MaximumEPFineEntries = 262144;
    internal const int MaximumATCSequences = 64;
    internal const int MaximumSTCSequences = 64;
    internal const int MaximumPrograms = 1024;
    internal const int MaximumStreamsInPS = 256;
}
```
