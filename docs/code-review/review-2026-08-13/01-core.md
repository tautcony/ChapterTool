# ChapterTool.Core 代码审查

审查日期：2026-08-13。审查对象：`src/ChapterTool.Core`（约 126 个文件，目标框架 net8.0/net9.0/net10.0，须兼容 Browser WebAssembly）。

## 审查过程（读了哪些区域、如何验证）

1. 先读 `docs/code-map/core.md` 建立模块索引，再用 `rg --files` 枚举全部源文件。
2. 逐文件通读以下区域（未抽样）：
   - 二进制光盘解析器：`Importing/Disc/` 下 MPLS 全部记录类型、`MplsBoundedStream`、`MplsParseLimits`、CLPI（`Clpi/`）、`index.bdmv`（`Index/`）、MovieObject 与 HDMV 导航解释器（`MovieObject/`）、BDJO（`Bdjo/`）、`IfoChapterImporter`、`XplChapterImporter`、`BdmvPathHelper`。
   - 文本/容器导入：OGM、WebVTT、Matroska XML、Premiere 标记、CUE（含 `CueTextDecoder`、`FlacCueImporter`、`TakCueImporter`）、`ChapterContentService` 分发逻辑、`SecureXmlLoader`。
   - 领域模型与变换：`Models/`、`Transform/`（时间格式化、帧率、表达式引擎、Lua 脚本）、`Editing/`、`Session/`、`Exporting/`（导出服务、编码、保存路径）、`Boundaries/PortableInputPolicy`。
3. 对疑点用独立 .NET 10 控制台程序做运行时验证（已删除临时工程），验证结论：
   - `TimeSpan.TryParse("01:30.500")` 与 `"10:03.500"`、`"25:00:00.000"` 均返回 `false`（WebVTT 短格式被拒）。
   - `ChapterTimeFormatter.Format` 逻辑复刻在 59.9996s 输入下输出 `00:00:60.000`。
   - `new UTF8Encoding(false, true).GetString(GBK 字节)` 抛出 `DecoderFallbackException`。
   - `uint` 回绕：`OUTTime(500) - INTime(1000)` 得 4294966796 PTS ≈ 26.5 小时。
   - `int.Parse("99999999999999")` 抛出 `OverflowException`。
   - XPL `tickBase=0`：`TimeSpan.TicksPerSecond / (0m / 1)` 抛出 `DivideByZeroException`。
   - `TimeSpan.FromSeconds(double.PositiveInfinity)` 抛出 `OverflowException`。
4. 用 `iconv -f UTF-8 -t UTF-8` 校验全部 `.cs` 文件均为合法 UTF-8；非 ASCII 字符仅出现在两个文件的注释/文档中，不影响产品字符串。

## 模块概览（简短）

- `Importing/`：按扩展名分发（`ChapterContentService`），文本导入器（OGM/WebVTT/XML/Premiere/CUE）与二进制光盘导入器（MPLS/CLPI/IFO/XPL/index/MovieObject/BDJO）。二进制解析统一通过 `MplsBoundedStream` 字节预算与各 `*ParseLimits` 上限防御。
- `Models/`：不可变 record（`Chapter`、`ChapterSet` 等）。
- `Transform/`：时间格式化（`ChapterTimeFormatter`）、帧率检测与转换、Lua 表达式引擎。
- `Editing/`、`Session/`：编辑操作与会话状态。
- `Exporting/`：多格式导出、输出编码、保存路径分配。

## 发现（按严重级别分组）

### 高

#### CORE-01：XPL 导入器在畸形数值属性上抛出未捕获异常（崩溃）

证据：`src/ChapterTool.Core/Importing/Disc/XplChapterImporter.cs`

```119:132:src/ChapterTool.Core/Importing/Disc/XplChapterImporter.cs
    private static TimeSpan ParseTime(string value, double timeBase, double tickBase, int tickBaseDivisor)
    {
        // ...
        var main = TimeSpan.Parse(value[..colon], CultureInfo.InvariantCulture);
        main = TimeSpan.FromSeconds(main.TotalSeconds / 60D * timeBase);
        var tickDuration = TimeSpan.TicksPerSecond / ((decimal)tickBase / tickBaseDivisor);
        var ticks = decimal.Parse(value[(colon + 1)..], CultureInfo.InvariantCulture) * tickDuration;
        return main.Add(TimeSpan.FromTicks((long)ticks));
    }
```

`ImportAsync` 的异常过滤器（第 57 行）只捕获 `FormatException`、`InvalidDataException`、`InvalidOperationException`、`XmlException`。以下运行时已验证的路径全部逃逸：

- `tickBase="0fps"`（或 `tickBaseDivisor` 使除数为 0）→ 第 129 行 `DivideByZeroException`。
- `timeBase="1e400fps"` → `double.Parse` 得 `Infinity`（第 116 行不抛），第 128 行 `TimeSpan.FromSeconds(Infinity)` 抛 `OverflowException`。
- 超大时间字段（如 `99999999999:00:00:00`）→ 第 127 行 `TimeSpan.Parse` 或第 130 行 `decimal.Parse`、第 131 行 `(long)ticks` 转换抛 `OverflowException`。

触发条件：一个畸形 `.xpl` 文件。后果：导入器违反"返回 Failed 结果"的契约，异常直接穿透到调用方。修复方向：在 `ParseFps` 中校验有限正值；把 `OverflowException`、`DivideByZeroException` 加入 catch 过滤器，或改用 `TryParse` + 范围校验。

#### CORE-02：CUE 导入器对非 UTF-8 编码文件直接崩溃

证据：`src/ChapterTool.Core/Importing/Cue/CueTextDecoder.cs`

```14:20:src/ChapterTool.Core/Importing/Cue/CueTextDecoder.cs
        return bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3),
            [0xFF, 0xFE, ..] => Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2),
            [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2),
            _ => new UTF8Encoding(false, true).GetString(bytes)
        };
```

无 BOM 分支使用 `throwOnInvalidBytes: true` 的 UTF-8 解码。历史 CUE 文件大量以 GBK/Shift-JIS/ANSI 编码保存，运行时验证 GBK 字节会抛 `DecoderFallbackException`。`CueChapterImporter.ImportAsync`（第 27–43 行）与 `ChapterContentService.ImportAsync` 均无捕获，异常直接穿透。`FlacCueImporter`/`TakCueImporter` 因内嵌 CUE 走 `Encoding.UTF8.GetString`（宽容模式）不受影响，行为还与 `.cue` 路径不一致。触发条件：导入任意 ANSI 编码 `.cue` 文件（真实世界常见）。修复方向：捕获 `DecoderFallbackException` 转为 `ChapterDiagnostic`，或回退到宽容解码并给出编码警告。

#### CORE-03：CueSheetParser 对超长数字未捕获 `OverflowException`（崩溃）

证据：`src/ChapterTool.Core/Importing/Cue/CueSheetParser.cs`

```47:52:src/ChapterTool.Core/Importing/Cue/CueSheetParser.cs
            if (trackMatch.Success)
            {
                currentNumber = int.Parse(trackMatch.Groups["Number"].Value, CultureInfo.InvariantCulture);
```

```126:133:src/ChapterTool.Core/Importing/Cue/CueSheetParser.cs
    private static TimeSpan ParseCueTime(Match match)
    {
        var minute = int.Parse(match.Groups["Minute"].Value, CultureInfo.InvariantCulture);
        // ...
        return new TimeSpan(0, 0, minute, second, millisecond);
    }
```

`TrackRegex` 的 `(?<Number>\d+)` 与 `IndexRegex` 的 `(?<Minute>\d{2,})` 均不限位数。`TRACK 99999999999999` 或 `INDEX 01 99999999999999:00:00` 使 `int.Parse` 抛 `OverflowException`（已验证）；即使数值在 `int` 内，超大分钟数也会让 `TimeSpan` 构造抛 `OverflowException`。整个调用链（`CueChapterImporter`、`FlacCueImporter`、`TakCueImporter`、`ChapterContentService`）都没有兜底捕获。修复方向：改用 `int.TryParse` + 上限校验，失败时记 `MalformedCueSyntax` 诊断。

#### CORE-04：MPLS `uint` 减法回绕导致时长与章节时间损坏

证据：`src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs`

```101:107:src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs
        var info = new ChapterSet(
            string.Empty,
            playItem.FullName,
            ChapterImportFormat.Mpls,
            MplsFrameRateCatalog.FromCode(playItem.STNTable.PrimaryVideoStreamEntries.FirstOrDefault()?.StreamAttributes.FrameRate),
            PtsToTime(playItem.OUTTime - playItem.INTime),
            chapters);
```

`src/ChapterTool.Core/Importing/Disc/MplsPlaylistProjection.cs`

```37:41:src/ChapterTool.Core/Importing/Disc/MplsPlaylistProjection.cs
        for (var index = 0; index < playItems.Count; index++)
        {
            starts[index] = cursor;
            cursor += playItems[index].OUTTime - playItems[index].INTime;
        }
```

```89:97:src/ChapterTool.Core/Importing/Disc/MplsPlaylistProjection.cs
        var offset = Math.Min(playItem.INTime, marks[0].MarkTimeStamp);
        return
        [
            .. marks
                .Select((mark, index) => new Chapter(
                    index + 1,
                    MplsChapterImporter.PtsToTime(mark.MarkTimeStamp - offset),
```

三处 `uint` 减法在畸形文件（`OUTTime < INTime`，或 mark 未按时间排序使后续 `MarkTimeStamp < offset`）下回绕。运行时验证 `500u - 1000u = 4294966796` PTS ≈ 26.5 小时。后果：单个 PlayItem 时长、累计起点 `starts`（污染后续所有 PlayItem 的章节偏移）以及导出的章节起始时间全部损坏，且与 CORE-06 叠加后导出文本进一步畸形。第 146–148 行的 `BuildPlaylistChapters` 已经用条件判断防护，证明此风险是已知模式，但上述三处漏掉了。修复方向：统一用"先比较后相减"（如 `outTime >= inTime ? outTime - inTime : 0`）并在回绕时记诊断。

#### CORE-05：WebVTT 导入器拒绝规范允许的时间戳格式（功能错误）

证据：`src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs`

```60:67:src/ChapterTool.Core/Importing/Text/WebVttChapterImporter.cs
            var parts = lines[0].Split("-->", StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TimeSpan.TryParse(parts[0], out var start) || !TimeSpan.TryParse(parts[1], out var end))
            {
                // ...
                return ChapterImportResult.Failed(Error(code, $"Unable to parse WebVTT timing line: {lines[0]}"));
            }
```

WebVTT 规范的时间戳为 `[hh:]mm:ss.ttt`，小时可省略，且小时可以 ≥ 24。运行时验证：`TimeSpan.TryParse("01:30.500")`、`"10:03.500"`、`"25:00:00.000"` 全部返回 `false`。任何使用短格式（YouTube 等工具导出的章节 VTT 常见）或超 24 小时时间戳的规范文件都会导致**整个导入失败**（第 66 行直接返回 Failed）。修复方向：按 WebVTT 语法自行解析（正则拆分 `hh`、`mm`、`ss.ttt`），不要依赖 `TimeSpan.TryParse`。

#### CORE-06：`ChapterTimeFormatter.Format` 产生非法时间文本（导出数据损坏）

证据：`src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs`

```17:28:src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs
    public string Format(TimeSpan time)
    {
        var millisecond = (int)Math.Round(
            (time.TotalSeconds - Math.Floor(time.TotalSeconds)) * 1000,
            MidpointRounding.ToEven);

        var seconds = millisecond == 1000
            ? $"{time.Seconds + 1:D2}.000"
            : $"{time.Seconds:D2}.{millisecond:D3}";

        return $"{time.Hours:D2}:{time.Minutes:D2}:{seconds}";
    }
```

三个独立缺陷：

1. 毫秒进位只加秒不进分：输入 59.9996s 时输出 `00:00:60.000`（运行时已验证）。`60` 秒是非法字段，OGM/qpfile 等下游工具会拒绝或错读。
2. `time.Hours` 是 24 小时内的分量：超过 24 小时的时间（例如 CORE-04 回绕产生的 26.5 小时，或多段拼接结果）丢失"天"分量，25 小时被写成 `01:xx:xx`。
3. 负 `TimeSpan`（见 CORE-08）时 `Hours/Minutes/Seconds` 为负数，输出形如 `00:-1:-2.-500` 的畸形文本。

修复方向：格式化前对毫秒做整体进位（用 `TimeSpan.FromMilliseconds(Math.Round(time.TotalMilliseconds))` 重建），用 `(int)time.TotalHours` 输出小时，负值时钳制为零并记诊断。

### 中

#### CORE-07：`FormatCue` 输出非法帧号 75，且分钟数在超长时间下回绕

证据：`src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs`

```67:76:src/ChapterTool.Core/Transform/ChapterTimeFormatter.cs
    public string FormatCue(TimeSpan time)
    {
        var frames = (int)Math.Round(time.Milliseconds * 75 / 1000F, MidpointRounding.ToEven);
        if (frames > 99)
        {
            frames = 99;
        }

        return $"{time.Hours * 60 + time.Minutes:D2}:{time.Seconds:D2}:{frames:D2}";
    }
```

CUE 帧号合法范围是 0–74。当毫秒 ∈ [994, 999] 时 `Math.Round(ms × 0.075)` 得 75，直接写入输出（例如 `00:01:75`），标准 CUE 消费方会拒绝。`frames > 99` 的钳制永远不可达（最大值 75），是失效的防护。另外 `time.Hours * 60` 同样丢失"天"分量。修复方向：帧数达到 75 时进位到秒；分钟改用 `(long)time.TotalMinutes`。

#### CORE-08：OGM 导入器产生负时间戳

证据：`src/ChapterTool.Core/Importing/Text/OgmChapterImporter.cs`

```54:55:src/ChapterTool.Core/Importing/Text/OgmChapterImporter.cs
        var firstTimeText = TimeValueRegex().Match(lines[0]).Value;
        var initialTime = timeFormatter.ParseOrZero(firstTimeText);
```

```76:76:src/ChapterTool.Core/Importing/Text/OgmChapterImporter.cs
                timeCode = timeFormatter.ParseOrZero(TimeValueRegex().Match(line).Value) - initialTime;
```

导入时无条件用首个时间戳归零。当后续时间戳小于首个（非单调输入，手工编辑文件中会出现）时 `timeCode` 为负 `TimeSpan`，随后进入模型并在导出时被 CORE-06 第 3 项格式化成畸形文本。触发条件：非单调 OGM 文件。修复方向：负值钳制为 `TimeSpan.Zero` 并记 `PartialParse`/警告诊断。

#### CORE-09：非 UTF-8 文本文件静默乱码，无任何诊断

证据：`src/ChapterTool.Core/Importing/Text/TextImportUtilities.cs`

```16:22:src/ChapterTool.Core/Importing/Text/TextImportUtilities.cs
        if (request.Content is not null)
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return await File.ReadAllTextAsync(request.Path, cancellationToken);
```

OGM/WebVTT/Premiere 等文本导入统一走此处，UTF-8 解码采用默认替换回退：GBK/Shift-JIS 编码的章节名会被静默替换成 U+FFFD，用户拿到乱码章节名却没有任何警告。与 CORE-02 的"直接崩溃"相比，这是同一编码问题的另一极端，两条路径行为也不一致。修复方向：先以 `throwOnInvalidBytes` 探测，解码失败时回退宽容解码并附加"编码可能不是 UTF-8"的警告诊断。

#### CORE-10：`XmlChapterImporter` 对不可寻址流抛 `NotSupportedException`

证据：`src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs`

```37:46:src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs
            try
            {
                var document = SecureXmlLoader.LoadXmlDocument(request.Content);
                request.Content.Position = 0;
                return ParseDocument(document, request.Path);
            }
            catch (XmlException exception)
            {
                return ChapterImportResult.Failed(Error(ChapterDiagnosticCode.InvalidXml, exception.Message));
            }
```

第 40 行在解析成功后把流位置重置为 0。若调用方传入不可寻址流（网络流、管道），`Position` 赋值抛 `NotSupportedException`，catch 只接 `XmlException`，异常穿透。而且该重置发生在文档已完整加载之后，看不出必要性。`ChapterImportRequest.Content` 的类型是任意 `Stream`，公共 API 不能假设可寻址。修复方向：删除该行，或先判断 `CanSeek`。

#### CORE-11：`FlacCueImporter` 对不可寻址流崩溃，且假设 `Stream.Read` 一次读满

证据：`src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs`

```63:84:src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs
        Span<byte> lengthBytes = stackalloc byte[3];
        while (stream.Position < stream.Length)
        {
            // ...
            var block = new byte[length];
            if (stream.Read(block) != length)
            {
                break;
            }
```

两个问题：(1) 第 64 行对不可寻址流访问 `Position`/`Length` 抛 `NotSupportedException`，`ImportAsync` 的 catch（第 42 行）只接特定消息的 `InvalidDataException`，异常穿透；(2) 第 58、72、81 行把 `Stream.Read` 的部分读取当作文件结束——`Read` 契约允许返回少于请求的字节数，元数据块跨缓冲边界时会静默丢弃后续块，返回"未找到 CUE"。修复方向：循环内改用 `ReadExactly`/`ReadAtLeast` 并以块头 `isLast` 标志（已有）替代 `Position < Length` 作为终止条件。

#### CORE-12：导入器对调用方流的所有权语义不一致（潜在双重释放/悬空流）

证据：

```34:34:src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs
        await using var stream = request.Content ?? File.OpenRead(request.Path);
```

```36:36:src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs
        await using var stream = request.Content ?? File.OpenRead(request.Path);
```

对比 `IfoChapterImporter.cs` 第 33–60 行刻意只释放自建流（`ownedStream = ReferenceEquals(stream, request.Content) ? null : stream`），文本导入器用 `leaveOpen: true`。MPLS 与 FLAC 两个导入器会释放**调用方传入的** `request.Content`。调用方（例如想在导入失败后用同一流重试其他格式，或 `ChapterContentService` 未来复用流）会遇到 `ObjectDisposedException`。当前 `ChapterContentService` 恰好每次新建 `MemoryStream` 才未暴露。修复方向：统一为"调用方拥有传入流"，MPLS/FLAC 改用 IFO 的所有权模式。

#### CORE-13：流式导入路径存在无上限内存缓冲（拒绝服务）

证据：`src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs`

```204:207:src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs
        var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
```

同模式还有 `CueChapterImporter.cs` 第 32–34 行、`TakCueImporter.cs` 第 38–40 行。`Boundaries/PortableInputPolicy` 已定义可移植输入大小上限，但这些拷贝路径没有应用它。通过流式 API（浏览器宿主、管道输入）传入超大内容时会无上限分配托管内存。`ChapterContentService.ImportAsync(byte[])` 路径由调用方限长，不受影响。修复方向：拷贝时施加 `PortableInputPolicy` 上限，超限返回诊断。

#### CORE-14：`XmlChapterImporter.ParseAtom` 无深度限制的递归（栈溢出，进程级崩溃）

证据：`src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs`

```184:186:src/ChapterTool.Core/Importing/Text/XmlChapterImporter.cs
                case "ChapterAtom":
                    inner.AddRange(ParseAtom(child, index));
                    break;
```

`ChapterAtom` 可自嵌套，递归无深度上限。恶意构造的深嵌套 XML（数万层，`SecureXmlLoader` 不限制元素深度）触发 `StackOverflowException`——该异常不可捕获，直接终止进程（桌面与 WASM 宿主均如此）。修复方向：加入显式深度计数（如上限 64 层），超限返回 `InvalidXml` 诊断；或改为显式栈迭代。

#### CORE-15：IFO 偏移量按有符号 16 位解释，≥0x8000 的合法偏移被误读

证据：`src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs`

```229:229:src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs
    private static short ToInt16(byte[] bytes) => (short)((bytes[0] << 8) + bytes[1]);
```

```149:150:src/ChapterTool.Core/Importing/Disc/IfoChapterImporter.cs
        var programMapOffset = ToInt16(ReadBlock(stream, pcgit + chainOffset + 230, 2));
        var cellTableOffset = ToInt16(ReadBlock(stream, pcgit + chainOffset + 0xE8, 2));
```

DVD 规范中 PGC 内偏移是无符号 16 位。`ToInt16` 对 ≥0x8000 的值做符号扩展得到负数：与 `pcgit + chainOffset` 相加后要么落在文件内的错误位置（静默解析出错误章节），要么为负触发 `InvalidDataException`。大型 PGC（cell 表超过 32 KB）是合法场景。修复方向：改为 `ushort`（返回 `int`）。

### 低

#### CORE-16：`EditTime` 静默把 ≥24 小时的输入清零，无诊断

证据：`src/ChapterTool.Core/Editing/ChapterEditingService.cs`

```30:33:src/ChapterTool.Core/Editing/ChapterEditingService.cs
        var parsed = timeFormatter.Parse(text);
        var value = parsed.Value >= TimeSpan.FromDays(1) ? TimeSpan.Zero : parsed.Value;
        chapters[index] = chapter with { StartTime = value };
```

用户输入 `25:00:00.000` 时章节时间被静默改为 0，`parsed.Diagnostics` 为空（解析本身成功），UI 层无从提示。修复方向：清零时附加一条 Warning 诊断。

#### CORE-17：FLAC/TAK 导入器保存了从不使用的 `parser` 字段

证据：`src/ChapterTool.Core/Importing/Cue/FlacCueImporter.cs` 第 11–13 行、`TakCueImporter.cs` 第 10–12 行：构造函数接受 `CueSheetParser?` 并存入 `parser` 字段，但两处实际都调用静态方法 `CueSheetParser.Parse`（Flac 第 52 行、Tak 第 58 行）。构造参数制造了"可注入解析器"的假象。修复方向：删除参数与字段，或真正使用实例。

#### CORE-18：MPLS 帧率码 5/8 映射到 30/60 fps，与 BD 规范保留值不符

证据：`src/ChapterTool.Core/Importing/Disc/MplsPlaylistProjection.cs`

```188:199:src/ChapterTool.Core/Importing/Disc/MplsPlaylistProjection.cs
    private static readonly double[] Values =
    [
        0,
        24000d / 1001d,
        24,
        25,
        30000d / 1001d,
        30,
        50,
        60000d / 1001d,
        60
    ];
```

BD 规范只定义帧率码 1–4、6、7；码 5、8 为保留值，此处映射为 30/60 fps，与 `Transform` 侧帧率选项中标注"Reserved"的口径不一致。畸形文件会得到一个看似合法的帧率而非 0/诊断。修复方向：保留码返回 0 并与 UI 口径对齐。

#### CORE-19：`ChapterSavePath` 在可移植核心中直接探测文件系统

证据：`src/ChapterTool.Core/Exporting/ChapterSavePath.cs` 第 51、59 行 `File.Exists`，第 143 行 `Directory.Exists`，第 85、142 行 `Path.GetFullPath`。这些托管 API 在 Browser WASM 下运行于内存虚拟文件系统，不会抛异常，但 `File.Exists` 恒为 `false`，唯一路径分配的防碰撞逻辑在浏览器宿主中形同虚设（真实保存发生在 JS/下载侧）。属于桌面语义泄入可移植层的维护风险，不是运行时错误。修复方向：把存在性探测收窄到桌面宿主（通过 `Infrastructure` 注入探测委托），Core 只负责纯字符串的文件名构造。

## 已排查、无问题

1. **MPLS/CLPI/index/BDJO 二进制越界与超大分配**：所有区段读取都经 `MplsBoundedStream` 字节预算与 `*ParseLimits` 上限（计数、长度均先校验再分配），未发现越界读取或无上限分配路径。
2. **XML 外部实体注入（XXE）**：`SecureXmlLoader` 统一 `DtdProcessing.Prohibit` + `XmlResolver = null`，XPL/Matroska XML 导入均经由它加载。
3. **HDMV 导航解释器无限循环**：`HdmvNavigation` 的解释循环有显式指令数/迭代上限，畸形 MovieObject 不会导致挂起。
4. **`PtsToTime` 毫秒进位**：`Math.Round` 可产生 1000 毫秒，但 `TimeSpan(0,0,0,s,1000)` 构造函数自动进位，不产生非法值（运行时验证）。
5. **源码 UTF-8 合法性**：全部 `.cs` 文件通过 `iconv` UTF-8 校验；非 ASCII 字符仅出现在 `ClipSession.cs`、`ChapterSavePath.cs` 的注释/文档中，无产品字符串风险。

## 修复优先级建议

1. **先堵崩溃入口（CORE-01、02、03、10、11）**：这五条都是"导入畸形/常见旧格式文件 → 未捕获异常穿透"，违反导入器统一返回 `ChapterImportResult` 的契约。建议统一做法：各解析器内部用 `TryParse` + 范围校验；`ChapterContentService.ImportAsync` 增加最外层兜底 catch，把意外异常转为 Error 诊断。CORE-02（ANSI 编码 CUE）在真实用户数据中触发概率最高，应最先修。
2. **再修输出正确性（CORE-06、07、04）**：`ChapterTimeFormatter` 是所有导出格式的汇聚点，`60.000`/负值/超 24 小时三类畸形输出会污染每种导出文件；MPLS 回绕是上游脏数据来源，两者一起修并补齐边界单元测试。
3. **然后是功能与健壮性（CORE-05、08、09、13、14、15）**：WebVTT 短格式（CORE-05）影响规范文件的可用性，优先级高于其余；编码回退（CORE-09）与 CORE-02 一起设计统一的文本解码策略。
4. **最后清理一致性问题（CORE-12、16、17、18、19）**：流所有权语义（CORE-12）建议在接口文档中明确约定后一次性对齐。
