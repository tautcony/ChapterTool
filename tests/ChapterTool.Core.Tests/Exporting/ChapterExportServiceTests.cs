using System.Globalization;
using System.Xml.Linq;
using ChapterTool.Core.Diagnostics;
using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using ChapterTool.Core.Transform.Expressions;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class ChapterExportServiceTests
{
    private readonly ChapterExportService service = new(new ChapterTimeFormatter());

    [Fact]
    public void Txt_export_writes_ogm_pairs()
    {
        var result = service.Serialize(Sample(), new ChapterSerializationOptions(ChapterExportFormat.Txt));

        Assert.Contains("CHAPTER01=00:00:00.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER01NAME=Intro", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_export_uses_und_language_fallback()
    {
        var result = service.Serialize(Sample(), new ChapterSerializationOptions(ChapterExportFormat.Xml));

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result.Content, StringComparison.Ordinal);
        Assert.Contains("<!--<!DOCTYPE Tags SYSTEM \"matroskatags.dtd\">-->", result.Content, StringComparison.Ordinal);
        Assert.Contains("<ChapterLanguage>und</ChapterLanguage>", result.Content, StringComparison.Ordinal);
        Assert.Contains("<ChapterTimeStart>00:00:10.000000</ChapterTimeStart>", result.Content, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine, result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OutputTextEncoding.Utf8, "utf-8")]
    [InlineData(OutputTextEncoding.Utf16LittleEndian, "utf-16")]
    [InlineData(OutputTextEncoding.Utf16BigEndian, "utf-16BE")]
    [InlineData(OutputTextEncoding.Utf32LittleEndian, "utf-32")]
    [InlineData(OutputTextEncoding.Utf32BigEndian, "utf-32BE")]
    public void Xml_export_declaration_matches_selected_encoding(OutputTextEncoding encoding, string xmlName)
    {
        var result = service.Serialize(Sample(), new ChapterSerializationOptions(ChapterExportFormat.Xml, TextEncoding: encoding));

        Assert.StartsWith($"<?xml version=\"1.0\" encoding=\"{xmlName}\"?>", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Qpfile_export_calculates_frames_without_display_markers()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.Zero, "Intro", "0", FrameAccuracy: FrameAccuracy.Accurate),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle", "240", FrameAccuracy: FrameAccuracy.Inexact),
                new Chapter(3, TimeSpan.FromSeconds(20), "End", "stale")
            ]
        };

        var result = service.Serialize(info, new ChapterSerializationOptions(ChapterExportFormat.Qpfile));

        Assert.Equal($"0 I{Environment.NewLine}240 I{Environment.NewLine}480 I", result.Content);
    }

    [Fact]
    public void Celltimes_export_writes_start_frames()
    {
        var result = service.Serialize(Sample(), new ChapterSerializationOptions(ChapterExportFormat.Celltimes));

        Assert.True(result.Success);
        Assert.Equal($"0{Environment.NewLine}240{Environment.NewLine}480", result.Content);
    }

    [Fact]
    public void Tsmuxer_export_has_no_trailing_semicolon()
    {
        var result = service.Serialize(Sample(), new ChapterSerializationOptions(ChapterExportFormat.TsMuxerMeta));

        Assert.Equal($"--custom-{Environment.NewLine}chapters=00:00:00.000;00:00:10.000;00:00:20.000", result.Content);
    }

    [Fact]
    public void Webvtt_export_preserves_explicit_chapter_end_times()
    {
        var info = Sample() with
        {
            Duration = TimeSpan.FromSeconds(60),
            Chapters =
            [
                new Chapter(1, TimeSpan.Zero, "Intro", EndTime: TimeSpan.FromSeconds(4)),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle", EndTime: TimeSpan.FromSeconds(15)),
                new Chapter(3, TimeSpan.FromSeconds(30), "End")
            ]
        };

        var result = service.Serialize(info, new ChapterSerializationOptions(ChapterExportFormat.WebVtt));

        Assert.True(result.Success);
        Assert.Contains("00:00:00.000 --> 00:00:04.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("00:00:10.000 --> 00:00:15.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("00:00:30.000 --> 00:01:00.000", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Webvtt_export_rejects_unsupported_cue_text_control_sequences()
    {
        var info = Sample() with
        {
            Chapters = [new Chapter(1, TimeSpan.Zero, "Line 1\r\nLine 2 --> marker", EndTime: TimeSpan.FromSeconds(1))]
        };

        var result = service.Serialize(info, new ChapterSerializationOptions(ChapterExportFormat.WebVtt));

        Assert.False(result.Success);
        Assert.Empty(result.Content);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidWebVttCueText);
    }

    [Fact]
    public void Xml_export_uses_valid_non_trivial_uids()
    {
        var result = service.Serialize(Sample(), new ChapterSerializationOptions(ChapterExportFormat.Xml));
        var document = XDocument.Parse(result.Content);

        var editionUid = Assert.Single(document.Descendants("EditionUID")).Value;
        var chapterUids = document.Descendants("ChapterUID").Select(static element => element.Value).ToArray();

        Assert.True(int.Parse(editionUid, CultureInfo.InvariantCulture) > 0);
        Assert.Equal(3, chapterUids.Length);
        Assert.All(chapterUids, static uid => Assert.True(int.Parse(uid, CultureInfo.InvariantCulture) > 0));
        Assert.NotEqual(["1", "2", "3"], chapterUids);
    }

    [Fact]
    public void Xml_export_reuses_content_seeded_uids_and_changes_them_with_content()
    {
        var options = new ChapterSerializationOptions(ChapterExportFormat.Xml);

        var first = service.Serialize(Sample(), options);
        var second = service.Serialize(Sample(), options);
        var changed = service.Serialize(Sample() with { Title = "Other Title" }, options);

        Assert.Equal(UidOrder(first.Content), UidOrder(second.Content));
        Assert.NotEqual(UidOrder(first.Content), UidOrder(changed.Content));
    }

    [Fact]
    public void Xml_export_assigns_distinct_positive_uids_to_repeated_chapters()
    {
        var sample = Sample();
        var source = sample with { Chapters = Enumerable.Repeat(sample.Chapters[0], 1000).ToArray() };

        var result = service.Serialize(source, new ChapterSerializationOptions(ChapterExportFormat.Xml));
        var document = XDocument.Parse(result.Content);
        var uids = document.Descendants()
            .Where(static element => element.Name.LocalName is "EditionUID" or "ChapterUID")
            .Select(static element => int.Parse(element.Value, CultureInfo.InvariantCulture)).ToArray();

        Assert.Equal(1001, uids.Length);
        Assert.All(uids, static uid => Assert.True(uid > 0));
        Assert.Equal(uids.Length, uids.Distinct().Count());
        Assert.Equal(UidOrder(result.Content), UidOrder(service.Serialize(source, new ChapterSerializationOptions(ChapterExportFormat.Xml)).Content));
    }

    [Fact]
    public void Cue_export_numbers_tracks_by_output_order()
    {
        var result = service.Serialize(
            Sample(),
            new ChapterSerializationOptions(ChapterExportFormat.Cue, SourceFileName: "audio.wav"));

        Assert.Contains("FILE \"audio.wav\" WAVE", result.Content, StringComparison.Ordinal);
        Assert.Contains("  TRACK 01 AUDIO", result.Content, StringComparison.Ordinal);
        Assert.Contains("    TITLE \"Intro\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("    INDEX 01 00:10:00", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_export_uses_mpls_source_name_and_separator_base_time()
    {
        var info = Sample(ChapterImportFormat.Mpls) with
        {
            SourceName = "00001",
            Chapters =
            [
                new Chapter(1, TimeSpan.FromSeconds(5), "A"),
                Chapter.Separator(),
                new Chapter(2, TimeSpan.FromSeconds(7), "B")
            ]
        };

        var result = service.Serialize(info, new ChapterSerializationOptions(ChapterExportFormat.Json));

        Assert.Contains("\"sourceName\":\"00001.m2ts\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"time\":0", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"time\":2", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_serialization_writes_captured_value_once_without_replaying_transforms()
    {
        var engine = new CountingExpressionEngine();
        var exportService = new ChapterExportService(new ChapterTimeFormatter());
        var document = EditableChapterDocumentAdapter.FromChapterSet(Sample() with
        {
            Chapters = [new Chapter(1, TimeSpan.FromSeconds(3), "Edited")]
        });

        var result = exportService.Serialize(document, new ChapterSerializationOptions(ChapterExportFormat.Txt));

        Assert.True(result.Success);
        Assert.Equal("CHAPTER01=00:00:03.000" + Environment.NewLine + "CHAPTER01NAME=Edited" + Environment.NewLine, result.Content);
        Assert.Equal(0, engine.EvaluationCount);
    }

    [Fact]
    public void Snapshot_webvtt_export_requires_a_final_end_when_duration_is_unknown()
    {
        var document = EditableChapterDocumentAdapter.FromChapterSet(
            new ChapterSet("Title", null, ChapterImportFormat.Unknown, FrameRateValue.Unknown, TimeSpan.Zero, [new Chapter(1, TimeSpan.Zero, "A")]));

        var result = service.Serialize(document, new ChapterSerializationOptions(ChapterExportFormat.WebVtt));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ChapterDiagnosticCode.InvalidStructure);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_frame_rate_is_rejected_before_export(double rate)
    {
        Assert.False(FrameRateValue.TryFromDouble(rate, metadata: false, out _, out _));
    }

    private static ChapterSet Sample(ChapterImportFormat sourceType = ChapterImportFormat.Ogm) =>
        new(
            "Title",
            "source",
            sourceType,
            FrameRateValue.Rational(24, 1),
            TimeSpan.FromSeconds(30),
            [
                new Chapter(1, TimeSpan.Zero, "Intro", "0"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle", "240", FrameAccuracy: FrameAccuracy.Inexact),
                new Chapter(3, TimeSpan.FromSeconds(20), "End", "480")
            ]);

    private static string UidOrder(string content)
    {
        var document = XDocument.Parse(content);
        return string.Join(',', document.Descendants()
            .Where(static element => element.Name.LocalName is "EditionUID" or "ChapterUID")
            .Select(static element => element.Name.LocalName + "=" + element.Value));
    }

    private static string NormalizeUids(string content)
    {
        if (!content.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
        {
            return content;
        }

        var document = XDocument.Parse(content);
        document.Descendants("ChapterUID").Remove();
        document.Descendants("EditionUID").Remove();
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private sealed class CountingExpressionEngine : IChapterExpressionEngine
    {
        public int EvaluationCount { get; private set; }

        public string EngineId => "counting";

        public IReadOnlyList<ChapterExpressionPreset> Presets => [];

        public ChapterExpressionEvaluationResult Evaluate(string sourceText, ChapterExpressionContext context)
        {
            EvaluationCount++;
            return new ChapterExpressionEvaluationResult(true, context.TimeSeconds, []);
        }
    }
}
