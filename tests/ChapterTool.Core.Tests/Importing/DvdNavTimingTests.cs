using System.Buffers.Binary;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Importing;

public sealed class DvdNavTimingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NavTimesCorrectNtscChaptersAcrossSplitVobsWrapAndCellClockReset(bool vmg, bool recoverPtt)
    {
        using var fixture = new DiscFixture();
        if (recoverPtt)
        {
            fixture.InvalidateFirstPttProgram();
        }

        var path = vmg ? fixture.VmgPath : fixture.IfoPath;
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(path), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var set = Assert.Single(Assert.Single(result.Groups).Entries).ChapterSet;
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromTicks(20_020_000)], set.Chapters.Select(chapter => chapter.StartTime));
        Assert.Equal(TimeSpan.FromTicks(30_030_000), set.Duration);
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingFromNav);
        Assert.Equal("VTS_05_0.IFO (VTS_05_1)", diagnostic.Arguments!["source"]);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingEstimated);
        var service = new FrameRateService();
        var display = service.UpdateFrames(set, service.FindByValue((decimal)set.FramesPerSecond), round: true, tolerance: 0.01m);
        Assert.Equal(["0", "60"], display.Chapters.Select(chapter => chapter.FramesInfo));
        Assert.All(display.Chapters, chapter => Assert.Equal(FrameAccuracy.Accurate, chapter.FrameAccuracy));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("truncated")]
    [InlineData("header")]
    [InlineData("cell")]
    [InlineData("sector")]
    [InlineData("time")]
    [InlineData("next")]
    [InlineData("discontinuity")]
    [InlineData("angle")]
    public async Task IncompleteNavPreservesEntireIfoTimelineAndReportsEstimate(string fault)
    {
        using var fixture = new DiscFixture();
        fixture.ApplyFault(fault);
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(fixture.IfoPath), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var set = Assert.Single(Assert.Single(result.Groups).Entries).ChapterSet;
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromTicks(30_030_000)], set.Chapters.Select(chapter => chapter.StartTime));
        Assert.Equal(TimeSpan.FromTicks(40_040_000), set.Duration);
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingEstimated);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingFromNav);
        var service = new FrameRateService();
        var display = service.UpdateFrames(set, service.FindByValue((decimal)set.FramesPerSecond), round: true, tolerance: 0.01m);
        Assert.Equal(FrameAccuracy.Accurate, display.Chapters[1].FrameAccuracy);
    }

    [Theory]
    [InlineData(1u, 20_020_111L, FrameAccuracy.Accurate)]
    [InlineData(1501u, 20_186_778L, FrameAccuracy.Inexact)]
    public async Task ValidNavFractionalFramesArePreservedWithoutSnapping(uint delta, long expectedTicks, FrameAccuracy accuracy)
    {
        using var fixture = new DiscFixture();

        // Preserve the measured time even when it falls between frame boundaries.
        fixture.SetNavEnd(2, unchecked(uint.MaxValue - 45_000u + 180_180u + delta));
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(fixture.IfoPath), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        var set = Assert.Single(result.Groups.Single().Entries).ChapterSet;
        Assert.Equal(expectedTicks, set.Chapters[1].StartTime.Ticks);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingFromNav);
        var service = new FrameRateService();
        var display = service.UpdateFrames(set, service.FindByValue((decimal)set.FramesPerSecond), round: true, tolerance: 0.01m);
        Assert.Equal(accuracy, display.Chapters[1].FrameAccuracy);
    }

    [Fact]
    public async Task TitleWithIncompletePgcPreservesIfoTimesForAllReferencedPgcs()
    {
        using var fixture = new DiscFixture();
        fixture.AddIncompleteSecondPgc();
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(fixture.IfoPath), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        var set = Assert.Single(result.Groups.Single().Entries).ChapterSet;
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromTicks(40_040_000)], set.Chapters.Select(chapter => chapter.StartTime));
        Assert.Equal(TimeSpan.FromTicks(80_080_000), set.Duration);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingEstimated);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingFromNav);
    }

    [Fact]
    public async Task StreamImportCannotReadSiblingVobs()
    {
        using var fixture = new DiscFixture();
        using var content = File.OpenRead(fixture.IfoPath);
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(fixture.IfoPath, content), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(TimeSpan.FromTicks(30_030_000), result.Groups.Single().Entries.Single().ChapterSet.Chapters[1].StartTime);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.IfoTimingEstimated);
    }

    [Fact]
    public void NavTraversalHonorsCancellation()
    {
        using var fixture = new DiscFixture();
        using var cancellation = new CancellationTokenSource();
        using var reader = DvdNavTimingReader.Open(fixture.IfoPath, cancellation.Token);
        Assert.NotNull(reader);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => reader.TryGetCellDuration(0, 2, 3, 1, 1, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IfoIntegerAccumulationDoesNotIncreaseLegacyNumericError(bool longCells)
    {
        using var fixture = new DiscFixture();
        fixture.UseManyIfoCells(longCells);
        using var content = File.OpenRead(fixture.IfoPath);
        var result = await new IfoChapterImporter().ImportAsync(new ChapterImportRequest(fixture.IfoPath, content), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        var set = Assert.Single(result.Groups.Single().Entries).ChapterSet;
        var framesPerCell = longCells ? 108001L : 1L;
        var legacyCell = TimeSpan.FromSeconds(framesPerCell / (30000d / 1001d));
        var legacyStart = TimeSpan.Zero;
        for (var index = 0; index < 99; index++)
        {
            legacyStart += legacyCell;
        }

        var exactStartTicks = framesPerCell * 99 * (decimal)TimeSpan.TicksPerSecond * 1001 / 30000;
        var exactDurationTicks = framesPerCell * 100 * (decimal)TimeSpan.TicksPerSecond * 1001 / 30000;
        Assert.InRange(Math.Abs(set.Chapters[1].StartTime.Ticks - exactStartTicks), 0, 0.5m);
        Assert.InRange(Math.Abs(set.Duration.Ticks - exactDurationTicks), 0, 0.5m);
        Assert.True(Math.Abs(set.Chapters[1].StartTime.Ticks - exactStartTicks) <= Math.Abs(legacyStart.Ticks - exactStartTicks));
        Assert.True(Math.Abs(set.Duration.Ticks - exactDurationTicks) <= Math.Abs((legacyStart + legacyCell).Ticks - exactDurationTicks));
        var rate = 30000m / 1001m;
        Assert.Equal(framesPerCell * 99, decimal.Round((decimal)set.Chapters[1].StartTime.TotalSeconds * rate));
    }

    private sealed class DiscFixture : IDisposable
    {
        private const int PgcStart = 4112;
        private const int CellTable = PgcStart + 238;
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("chaptertool-nav-");
        private readonly byte[] ifo = new byte[8192];
        private readonly byte[] vob = new byte[6 * 2048];

        public DiscFixture()
        {
            "DVDVIDEO-VTS"u8.CopyTo(ifo);
            Write32(ifo, 0xC8, 1);
            Write32(ifo, 0xCC, 2);
            Write16(ifo, 2048, 1);
            Write32(ifo, 2052, 19);
            Write32(ifo, 2056, 12);
            Write16(ifo, 2060, 1);
            Write16(ifo, 2062, 1);
            Write16(ifo, 2064, 1);
            Write16(ifo, 2066, 2);
            Write16(ifo, 4096, 1);
            Write32(ifo, 4100, 309);
            Write32(ifo, 4108, 16);
            ifo[PgcStart + 2] = 2;
            ifo[PgcStart + 3] = 2;
            Write16(ifo, PgcStart + 0xE6, 236);
            Write16(ifo, PgcStart + 0xE8, 238);
            Write16(ifo, PgcStart + 0xEA, 286);
            ifo[PgcStart + 236] = 1;
            ifo[PgcStart + 237] = 2;
            SetCell(0, 3, 0, 2, 3);
            SetCell(1, 1, 4, 4, 5);

            var start = uint.MaxValue - 45_000;
            SetNav(0, start, unchecked(start + 90_090), 1, 2);
            SetNav(2, unchecked(start + 90_090), unchecked(start + 180_180), 1, 0x3FFF_FFFF);
            SetNav(4, 90_000, 180_090, 2, 0x3FFF_FFFF);
            Save();

            var vmg = new byte[4096];
            "DVDVIDEO-VMG"u8.CopyTo(vmg);
            Write32(vmg, 0xC4, 1);
            Write16(vmg, 2048, 1);
            Write32(vmg, 2052, 19);
            vmg[2057] = 1;
            Write16(vmg, 2058, 2);
            vmg[2062] = 5;
            vmg[2063] = 1;
            File.WriteAllBytes(VmgPath, vmg);
        }

        public string IfoPath => Path.Combine(directory.FullName, "VTS_05_0.IFO");

        public string VmgPath => Path.Combine(directory.FullName, "VIDEO_TS.IFO");

        public void UseManyIfoCells(bool longCells)
        {
            ifo[PgcStart + 3] = 100;
            ifo[PgcStart + 237] = 100;
            Write16(ifo, PgcStart + 0xEA, 2638);
            Write32(ifo, 4100, 3053);
            Array.Clear(ifo, CellTable, 2400);
            for (var index = 0; index < 100; index++)
            {
                ifo[CellTable + index * 24 + 4] = longCells ? (byte)1 : (byte)0;
                ifo[CellTable + index * 24 + 7] = 0xC1;
            }

            Save();
        }

        public void InvalidateFirstPttProgram()
        {
            Write16(ifo, 2062, 0);
            Save();
        }

        public void AddIncompleteSecondPgc()
        {
            var originalPgc = ifo.AsSpan(PgcStart, 294).ToArray();
            Write16(ifo, 4096, 2);
            Write32(ifo, 4100, 611);
            Write32(ifo, 4108, 24);
            Write32(ifo, 4116, 318);
            originalPgc.CopyTo(ifo, 4120);
            originalPgc.CopyTo(ifo, 4414);
            Write32(ifo, 4414 + 238 + 8, 999);
            Write16(ifo, 2064, 2);
            Write16(ifo, 2066, 1);
            Save();
        }

        public void SetNavEnd(int sector, uint end)
        {
            Write32(vob, sector * 2048 + 61, end);
            Save();
        }

        public void ApplyFault(string fault)
        {
            switch (fault)
            {
                case "missing": File.Delete(Path.Combine(directory.FullName, "VTS_05_1.VOB")); return;
                case "empty": File.WriteAllBytes(Path.Combine(directory.FullName, "VTS_05_2.VOB"), []); return;
                case "truncated": File.WriteAllBytes(Path.Combine(directory.FullName, "VTS_05_2.VOB"), new byte[2047]); return;
                case "header": vob[4 * 2048 + 44] = 1; break;
                case "cell": vob[4 * 2048 + 1058] = 1; break;
                case "sector": Write32(vob, 4 * 2048 + 1035, 999); break;
                case "time": Write32(vob, 4 * 2048 + 61, 90_000); break;
                case "next": Write32(vob, 1345, 0); break;
                case "discontinuity": Write32(vob, 2 * 2048 + 57, 60_000); break;
                case "angle": ifo[CellTable] = 0x50; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }

            Save();
        }

        public void Dispose() => directory.Delete(recursive: true);

        private void SetCell(int index, byte seconds, uint first, uint lastVobu, uint last)
        {
            var offset = CellTable + index * 24;
            ifo[offset + 6] = seconds;
            ifo[offset + 7] = 0xC0;
            Write32(ifo, offset + 8, first);
            Write32(ifo, offset + 16, lastVobu);
            Write32(ifo, offset + 20, last);
            Write16(ifo, PgcStart + 286 + index * 4, 1);
            ifo[PgcStart + 289 + index * 4] = (byte)(index + 1);
        }

        private void SetNav(int sector, uint start, uint end, byte cell, uint next)
        {
            var offset = sector * 2048;
            new byte[] { 0, 0, 1, 0xBA, 0x44 }.CopyTo(vob, offset);
            new byte[] { 0, 0, 1, 0xBB }.CopyTo(vob, offset + 14);
            Write16(vob, offset + 18, 18);
            new byte[] { 0, 0, 1, 0xBF }.CopyTo(vob, offset + 38);
            Write16(vob, offset + 42, 980);
            Write32(vob, offset + 45, (uint)sector);
            Write32(vob, offset + 57, start);
            Write32(vob, offset + 61, end);
            new byte[] { 0, 0, 1, 0xBF }.CopyTo(vob, offset + 1024);
            Write16(vob, offset + 1028, 1018);
            vob[offset + 1030] = 1;
            Write32(vob, offset + 1035, (uint)sector);
            Write32(vob, offset + 1039, 1);
            Write16(vob, offset + 1055, 1);
            vob[offset + 1058] = cell;
            Write32(vob, offset + 1345, next);
        }

        private void Save()
        {
            File.WriteAllBytes(IfoPath, ifo);
            File.WriteAllBytes(Path.Combine(directory.FullName, "VTS_05_1.VOB"), vob[..(3 * 2048)]);
            File.WriteAllBytes(Path.Combine(directory.FullName, "VTS_05_2.VOB"), vob[(3 * 2048)..]);
        }

        private static void Write16(byte[] data, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), value);

        private static void Write32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), value);
    }
}
