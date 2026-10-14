using System.Numerics;
using ChapterTool.Core.Editing;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Importing;
using ChapterTool.Core.Importing.Disc;
using ChapterTool.Core.Models;
using ChapterTool.Core.Session;
using ChapterTool.Core.Transform;

namespace ChapterTool.Core.Tests.Editing;

public sealed class ExactFrameRateWorkflowTests(ITestOutputHelper output)
{
    private static readonly ChapterContentCandidateBuilder Builder = new(new ChapterEditingService(new ChapterTimeFormatter()));

    [Theory]
    [InlineData("Mpls", "00001_fch.mpls")]
    [InlineData("Ifo", "VTS_05_0.IFO")]
    public async Task Disc_import_fractional_edit_review_history_conversion_and_export(string folder, string file)
    {
        IChapterImporter importer = folder == "Mpls" ? new MplsChapterImporter() : new IfoChapterImporter();
        var imported = await importer.ImportAsync(new ChapterImportRequest(FixtureResolver.Fixture("Importing", "Disc", folder, file)),
            TestContext.Current.CancellationToken);
        Assert.True(imported.Success);
        var group = Assert.Single(imported.Groups);
        var root = EditableChapterDocumentAdapter.FromChapterImportSource(new ChapterImportSource(group.SourcePath, [group.Entries[0]]));
        var rate = root.FrameRate;
        Assert.Equal(FrameRateKind.Rational, rate.Kind);
        var session = new SessionState(root);
        var edited = Builder.EditCell(root, root.Tracks[0].Chapters[0].Id, ChapterCellField.Frame, "1.5", rate);
        Assert.True(edited.IsValid, string.Join(";", edited.Errors));
        Assert.Same(root, session.Snapshot.Document);
        var ratio = rate.RationalRate!.Value;
        Assert.Equal(Round((BigInteger)15 * TimeSpan.TicksPerSecond * ratio.Denominator, (BigInteger)10 * ratio.Numerator),
            edited.Candidate.Tracks[0].Chapters[0].StartTicks);
        await Commit(session, edited);
        var approximate = FrameRateValue.Approximate(25m);
        await Commit(session, Builder.ChangeFrameRate(session.Snapshot.Document, rate, approximate));
        await Commit(session, Builder.ChangeFrameRate(session.Snapshot.Document, approximate, rate));
        var reviewed = session.Snapshot.Document;
        Assert.Equal(root.Tracks[0].Chapters.Select(chapter => chapter.Id), reviewed.Tracks[0].Chapters.Select(chapter => chapter.Id));
        Assert.Equal(root.Tracks[0].Segments.Select(segment => segment.SourceFrameRate), reviewed.Tracks[0].Segments.Select(segment => segment.SourceFrameRate));
        var token = session.Snapshot.BaseToken;
        var exporter = new ChapterExportService(new ChapterTimeFormatter());
        var preview = exporter.Serialize(EditableChapterDocumentAdapter.ToChapterSet(reviewed), new ChapterSerializationOptions(ChapterExportFormat.Qpfile));
        Assert.True(preview.Success);
        Assert.Equal(string.Join(Environment.NewLine, reviewed.Tracks[0].Chapters.Select(chapter =>
            $"{Round((BigInteger)chapter.StartTicks * ratio.Numerator, (BigInteger)TimeSpan.TicksPerSecond * ratio.Denominator)} I")), preview.Content);
        Assert.Equal(token, session.Snapshot.BaseToken);
        for (var index = 0; index < 3; index++)
        {
            await session.UndoAsync();
        }
        Assert.True(ExpressionPreviewProjector.DocumentsEqual(root, session.Snapshot.Document));
        for (var index = 0; index < 3; index++)
        {
            await session.RedoAsync();
        }
        Assert.True(ExpressionPreviewProjector.DocumentsEqual(reviewed, session.Snapshot.Document));
        var saved = exporter.Serialize(EditableChapterDocumentAdapter.ToChapterSet(session.Snapshot.Document), new ChapterSerializationOptions(ChapterExportFormat.Qpfile));
        Assert.Equal(preview.Content, saved.Content);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Five_hundred_round_trips_with_multiplication_division_lose_less_than_one_frame(bool approximateSource, bool approximateTarget)
    {
        var source = approximateSource ? FrameRateValue.Approximate(23.976m) : FrameRateValue.Rational(24000, 1001);
        var target = approximateTarget ? FrameRateValue.Approximate(29.97m) : FrameRateValue.Rational(30000, 1001);
        long[] ticks = [1, 12_345_678, 863_999_999_999, 9_007_199_254_740_993];
        var trackId = ChapterTrackId.New();
        var document = new EditableChapterDocument(ChapterDocumentId.New(), "Precision", null, ChapterImportFormat.Ogm,
            ChapterDuration.Unknown, [new EditableChapterTrack(trackId, "Track",
                ticks.Select((value, index) => new EditableChapter(ChapterId.New(), index + 1, value, $"Chapter {index + 1}")), frameRate: source)]);
        var (numerator, denominator) = approximateSource ? ((BigInteger)23976, (BigInteger)1000) : ((BigInteger)24000, (BigInteger)1001);
        BigInteger maximumLoss = 0;
        for (var cycle = 0; cycle < 500; cycle++)
        {
            document = Valid(Builder.ChangeFrameRate(document, source, target));
            document = Valid(Builder.ApplyExpression(document, "t * 1001 / 1000"));
            document = Valid(Builder.ApplyExpression(document, "t * 1000 / 1001"));
            document = Valid(Builder.ChangeFrameRate(document, target, source));
            Assert.Equal(source, document.FrameRate);
            for (var index = 0; index < ticks.Length; index++)
            {
                var loss = BigInteger.Abs((BigInteger)document.Tracks[0].Chapters[index].StartTicks - ticks[index]);
                maximumLoss = BigInteger.Max(maximumLoss, loss);
                Assert.True(loss * numerator < TimeSpan.TicksPerSecond * denominator,
                    $"Cycle {cycle + 1}, marker {index}: {loss} ticks lost at {source}.");
            }
        }

        output.WriteLine($"500 cycles ({source.Kind} -> {target.Kind} -> {source.Kind}), 2000 operations: maximum loss {maximumLoss} ticks; {(decimal)maximumLoss * (decimal)numerator / (TimeSpan.TicksPerSecond * (decimal)denominator):G17} frames.");
    }

    private static EditableChapterDocument Valid(ChapterCandidateBuildResult result)
    {
        Assert.True(result.IsValid, string.Join(";", result.Errors));
        return result.Candidate;
    }

    private static async Task Commit(SessionState session, ChapterCandidateBuildResult candidate)
    {
        Assert.True(candidate.IsValid, string.Join(";", candidate.Errors));
        var outcome = await session.ExecuteAsync(session.Snapshot.BaseToken, Guid.NewGuid(), "reviewed operation",
            (_, _) => ValueTask.FromResult(candidate.Candidate));
        Assert.Equal(TransactionOutcomeKind.Committed, outcome.Kind);
        Assert.True(ExpressionPreviewProjector.DocumentsEqual(candidate.Candidate, outcome.Snapshot.Document));
    }

    private static long Round(BigInteger numerator, BigInteger denominator)
    {
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        return checked((long)(quotient + (remainder * 2 >= denominator ? 1 : 0)));
    }
}
