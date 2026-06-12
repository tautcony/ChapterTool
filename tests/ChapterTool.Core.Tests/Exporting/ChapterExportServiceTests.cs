using ChapterTool.Core.Exporting;
using ChapterTool.Core.Models;
using ChapterTool.Core.Transform;
using System.Globalization;
using System.Xml.Linq;

namespace ChapterTool.Core.Tests.Exporting;

public sealed class ChapterExportServiceTests
{
    private readonly ChapterExportService service = new(new ChapterTimeFormatter(), new ExpressionService());

    [Fact]
    public void Txt_export_writes_ogm_pairs()
    {
        var result = service.Export(Sample(), new ChapterExportOptions(ChapterExportFormat.Txt));

        Assert.Contains("CHAPTER01=00:00:00.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER01NAME=Intro", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_export_uses_und_language_fallback()
    {
        var result = service.Export(Sample(), new ChapterExportOptions(ChapterExportFormat.Xml));

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result.Content, StringComparison.Ordinal);
        Assert.Contains("<!--<!DOCTYPE Tags SYSTEM \"matroskatags.dtd\">-->", result.Content, StringComparison.Ordinal);
        Assert.Contains("<ChapterLanguage>und</ChapterLanguage>", result.Content, StringComparison.Ordinal);
        Assert.Contains("<ChapterTimeStart>00:00:10.000000</ChapterTimeStart>", result.Content, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine, result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Qpf_export_trims_markers_and_appends_I()
    {
        var result = service.Export(Sample(), new ChapterExportOptions(ChapterExportFormat.Qpf));

        Assert.Equal($"0 I{Environment.NewLine}240 I{Environment.NewLine}480 I", result.Content);
    }

    [Fact]
    public void Celltimes_export_writes_start_frames()
    {
        var result = service.Export(Sample(), new ChapterExportOptions(ChapterExportFormat.Celltimes));

        Assert.True(result.Success);
        Assert.Equal($"0{Environment.NewLine}240{Environment.NewLine}480", result.Content);
    }

    [Fact]
    public void Chapter2Qpfile_export_converts_chapter_times_to_qpf()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.Zero, "Intro", "stale"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle", "stale")
            ]
        };

        var result = service.Export(info, new ChapterExportOptions(ChapterExportFormat.Chapter2Qpfile));

        Assert.True(result.Success);
        Assert.Equal($"0 I{Environment.NewLine}240 I", result.Content);
    }

    [Fact]
    public void Tsmuxer_export_has_no_trailing_semicolon()
    {
        var result = service.Export(Sample(), new ChapterExportOptions(ChapterExportFormat.TsMuxerMeta));

        Assert.Equal($"--custom-{Environment.NewLine}chapters=00:00:00.000;00:00:10.000;00:00:20.000", result.Content);
    }

    [Fact]
    public void Txt_export_applies_order_shift_and_generated_names()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Txt, AutoGenerateNames: true, OrderShift: 2));

        Assert.Contains("CHAPTER03=00:00:00.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER03NAME=Chapter 01", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER05=00:00:20.000", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER01=", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_export_applies_order_shift_language_and_template_names()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Xml, XmlLanguage: "jpn", UseTemplateNames: true, OrderShift: 4));

        Assert.Contains("<ChapterLanguage>jpn</ChapterLanguage>", result.Content, StringComparison.Ordinal);
        Assert.Contains("<ChapterString>Chapter 01</ChapterString>", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("<ChapterString>Intro</ChapterString>", result.Content, StringComparison.Ordinal);

        var document = XDocument.Parse(result.Content);
        var chapterUid = Assert.Single(document.Descendants("ChapterUID").Take(1)).Value;
        Assert.True(int.Parse(chapterUid, CultureInfo.InvariantCulture) > 0);
        Assert.NotEqual("5", chapterUid);
    }

    [Fact]
    public void Xml_export_uses_valid_non_trivial_uids()
    {
        var result = service.Export(Sample(), new ChapterExportOptions(ChapterExportFormat.Xml));
        var document = XDocument.Parse(result.Content);

        var editionUid = Assert.Single(document.Descendants("EditionUID")).Value;
        var chapterUids = document.Descendants("ChapterUID").Select(static element => element.Value).ToArray();

        Assert.True(int.Parse(editionUid, CultureInfo.InvariantCulture) > 1);
        Assert.Equal(3, chapterUids.Length);
        Assert.NotEqual(["1", "2", "3"], chapterUids);
    }

    [Fact]
    public void Txt_export_applies_template_file_names_by_line()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(
                ChapterExportFormat.Txt,
                UseTemplateNames: true,
                ChapterNameTemplateText: "Opening\nMiddle Part\nFinale"));

        Assert.Contains("CHAPTER01NAME=Opening", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER02NAME=Middle Part", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER03NAME=Finale", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Negative_order_shift_is_normalized_to_zero()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Txt, OrderShift: -2));

        Assert.Contains("CHAPTER01=00:00:00.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER03=00:00:20.000", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER00", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER-01", result.Content, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OrderShiftNormalized");
    }

    [Fact]
    public void Order_shift_ignores_separator_markers()
    {
        var info = Sample() with
        {
            Chapters =
            [
                new Chapter(1, TimeSpan.Zero, "A", "0 K"),
                new Chapter(-1, Chapter.SeparatorTime, ""),
                new Chapter(2, TimeSpan.FromSeconds(7), "B", "168 K")
            ]
        };

        var result = service.Export(info, new ChapterExportOptions(ChapterExportFormat.Txt, OrderShift: 2));

        Assert.Contains("CHAPTER03=00:00:00.000", result.Content, StringComparison.Ordinal);
        Assert.Contains("CHAPTER04=00:00:07.000", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER-01", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("CHAPTER05", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Cue_export_numbers_tracks_by_output_order()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Cue, SourceFileName: "audio.wav", AutoGenerateNames: true));

        Assert.Contains("FILE \"audio.wav\" WAVE", result.Content, StringComparison.Ordinal);
        Assert.Contains("  TRACK 01 AUDIO", result.Content, StringComparison.Ordinal);
        Assert.Contains("    TITLE \"Chapter 01\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("    INDEX 01 00:10:00", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_export_uses_mpls_source_name_and_separator_base_time()
    {
        var info = Sample("MPLS") with
        {
            SourceName = "00001",
            Chapters =
            [
                new Chapter(1, TimeSpan.FromSeconds(5), "A"),
                new Chapter(-1, Chapter.SeparatorTime, ""),
                new Chapter(2, TimeSpan.FromSeconds(7), "B")
            ]
        };

        var result = service.Export(info, new ChapterExportOptions(ChapterExportFormat.Json));

        Assert.Contains("\"sourceName\":\"00001.m2ts\"", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"time\":0", result.Content, StringComparison.Ordinal);
        Assert.Contains("\"time\":2", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Timecodes_export_applies_expression_when_enabled()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.TimeCodes, ApplyExpression: true, Expression: "t + 1"));

        Assert.StartsWith("00:00:01.000", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Expression_export_normalizes_negative_times_to_zero()
    {
        var info = Sample() with
        {
            Chapters = [new Chapter(1, TimeSpan.FromSeconds(10), "Middle", "240 K")]
        };
        var result = service.Export(
            info,
            new ChapterExportOptions(ChapterExportFormat.Txt, ApplyExpression: true, Expression: "t - 10000"));

        Assert.Contains("CHAPTER01=00:00:00.000", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("-", result.Content, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "InvalidExpressionTime");
    }

    [Fact]
    public void Cue_export_applies_expression_when_enabled()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Cue, ApplyExpression: true, Expression: "t + 1"));

        Assert.Contains("    INDEX 01 00:01:00", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Qpf_export_uses_expression_adjusted_frames_when_enabled()
    {
        var result = service.Export(
            Sample(),
            new ChapterExportOptions(ChapterExportFormat.Qpf, ApplyExpression: true, Expression: "t + 1"));

        Assert.StartsWith("24 I", result.Content, StringComparison.Ordinal);
    }

    private static ChapterInfo Sample(string sourceType = "OGM") =>
        new(
            "Title",
            "source",
            0,
            sourceType,
            24,
            TimeSpan.FromSeconds(30),
            [
                new Chapter(1, TimeSpan.Zero, "Intro", "0 K"),
                new Chapter(2, TimeSpan.FromSeconds(10), "Middle", "240 *"),
                new Chapter(3, TimeSpan.FromSeconds(20), "End", "480 K")
            ]);
}
