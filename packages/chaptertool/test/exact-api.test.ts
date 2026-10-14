import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";
import { ChapterTool } from "@chaptertool/node";
import type { ExactChapterSet, ExactFrameRate, ExactImportGroup } from "@chaptertool/node";

const rational: ExactFrameRate = { kind: "rational", numerator: "48000", denominator: "2002" };
const approximate: ExactFrameRate = { kind: "approximate", value: "23.976" };
const unknown: ExactFrameRate = { kind: "unknown" };
const tool = new ChapterTool();
function sample(rate: ExactFrameRate = rational, duration: string | null = "100000000"): ExactChapterSet {
  return {
    title: "Exact", sourceName: null, importFormat: "Mpls", frameRate: rate, durationTicks: duration,
    chapters: [{ displayNumber: 1, startTicks: "0", endTicks: null, name: "A", framesInfo: "", frameAccuracy: "Neutral", kind: "Marker" }],
  };
}
function group(set: ExactChapterSet): ExactImportGroup {
  return { sourcePath: "source.mpls", defaultEntryIndex: 0, entries: [
    { id: "one", displayName: "One", chapterSet: set, canCombine: true, referencedMediaFiles: null },
  ] };
}

describe("packaged exact facade", () => {
  it("imports rational IFO metadata and unknown text without numeric downgrade", async () => {
    const bytes = await readFile(new URL("../../../tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Ifo/VTS_05_0.IFO", import.meta.url));
    const imported = await tool.import(bytes, { fileName: "VTS_05_0.IFO" });
    expect(imported.success).toBe(true);
    const set = imported.groups[0].entries[0].chapterSet;
    expect(set.frameRate.kind).toBe("rational");
    const updated = await tool.editFrame(set, 0, "1.5");
    expect(updated.success).toBe(true);
    expect(updated.chapterSet!.frameRate).toEqual(set.frameRate);
    expect(BigInt(updated.chapterSet!.chapters[0].startTicks)).toBeGreaterThan(0n);
    expect((await tool.serialize(updated.chapterSet!, { format: "qpf" })).success).toBe(true);
    const text = await tool.import("CHAPTER01=00:00:00.000\nCHAPTER01NAME=A\n");
    expect(text.groups[0].entries[0].chapterSet.frameRate).toEqual(unknown);
  });

  it("reduces ratios and preserves tick strings above the JS safe-integer limit", async () => {
    const set = sample(rational, null);
    set.chapters[0].startTicks = "9007199254740993";
    set.chapters[0].endTicks = "9007199254741093";
    const renamed = await tool.rename(set, 0, "B");
    expect(renamed.success).toBe(true);
    expect(renamed.chapterSet!.frameRate).toEqual({ kind: "rational", numerator: "24000", denominator: "1001" });
    expect(renamed.chapterSet!.durationTicks).toBeNull();
    expect(renamed.chapterSet!.chapters[0].startTicks).toBe(set.chapters[0].startTicks);
    expect(renamed.chapterSet!.chapters[0].endTicks).toBe(set.chapters[0].endTicks);
    const unchanged = await tool.applyExpression(renamed.chapterSet!, true, "chapter.time");
    expect(unchanged.success).toBe(true);
    expect(unchanged.chapterSet!.chapters[0].startTicks).toBe(set.chapters[0].startTicks);
  });

  it("preserves huge rational components as strings and only exposes safe Lua numeric components", async () => {
    const rate: ExactFrameRate = { kind: "rational", numerator: "9007199254740993", denominator: "1001" };
    const result = await tool.applyExpression(sample(rate), true,
      'fpsNumerator == nil and fpsDenominator == nil and fpsNumeratorText == "9007199254740993" and t or nil');
    expect(result.success).toBe(true);
    expect(result.chapterSet!.frameRate).toEqual(rate);
  });

  it.each([null, "0"])("preserves duration availability %s and nullable ends across edits", async (duration) => {
    const result = await tool.rename(sample(approximate, duration), 0, "B");
    expect(result.chapterSet!.durationTicks).toBe(duration);
    expect(result.chapterSet!.chapters[0].endTicks).toBeNull();
    expect(result.chapterSet!.frameRate).toEqual(approximate);
    const changed = await tool.changeFrameRate(result.chapterSet!, approximate, { kind: "rational", numerator: "25", denominator: "1" });
    expect(changed.success).toBe(true);
    expect(changed.chapterSet!.durationTicks).toBe(duration);
  });

  it.each([
    {}, { kind: "other" }, { kind: "unknown", value: "0" },
    { kind: "rational", numerator: "1", denominator: "0" },
    { kind: "rational", numerator: "9223372036854775808", denominator: "1" },
    { kind: "approximate", value: "-24" }, { kind: "approximate", value: "1,000" },
    { kind: "approximate", value: "0.00000000000000000000000000001" },
    { kind: "approximate", value: "23.97600000000000000000000000001" },
  ])("rejects malformed rate %j with structured diagnostics", async (payload) => {
    const result = await tool.rename(sample(payload as ExactFrameRate), 0, "B");
    expect(result.success).toBe(false);
    expect(result.chapterSet).toBeNull();
    expect(result.diagnostics[0].code).toBe("FrameRate.Invalid");
  });

  it("keeps Auto detection display-only and serializes an explicitly transformed snapshot", async () => {
    const set = sample(unknown);
    const framed = await tool.updateFrames(set);
    expect(framed.success).toBe(true);
    expect(framed.selectedOption!.rate.kind).toBe("rational");
    expect(framed.chapterSet!.frameRate).toEqual(unknown);
    expect((await tool.serialize(set, { format: "txt" })).success).toBe(true);
    expect((await tool.serialize(set, { format: "qpf" })).success).toBe(false);
    const options = { format: "qpf", computationRate: rational };
    const transformed = await tool.applyExpression(set, true, "t + 1 / fps", rational);
    expect(transformed.success).toBe(true);
    const serialized = await tool.serialize(transformed.chapterSet!, options);
    expect(serialized.success).toBe(true);
    expect(serialized.content).toContain("1 I");
    expect(set.chapters[0].startTicks).toBe("0");
    expect(transformed.chapterSet!.frameRate).toEqual(unknown);
  });

  it("converts both rate kinds and exposes shifts, zones, detection and conversion tools", async () => {
    const edited = await tool.editFrame(sample(rational), 0, "240");
    const changed = await tool.changeFrameRate(edited.chapterSet!, rational, approximate);
    expect(changed.success).toBe(true);
    expect(changed.chapterSet!.frameRate).toEqual(approximate);
    const shifted = await tool.shiftFramesForward(changed.chapterSet!, -1);
    expect(shifted.success).toBe(true);
    expect((await tool.createZones(shifted.chapterSet!, [0])).zones).toContain("--zones");
    expect((await tool.detectFrameRate(shifted.chapterSet!)).success).toBe(true);
    expect((await tool.toCelltimes(shifted.chapterSet!)).success).toBe(true);
    expect((await tool.frameRates())[1].rate).toEqual({ kind: "rational", numerator: "24000", denominator: "1001" });
    const mapped = await tool.chapterTextToQpfile("CHAPTER01=00:00:00.000\nCHAPTER01NAME=A\n", undefined,
      "# timecode format v2\n0\n40\n80\n");
    expect(mapped.success).toBe(true);
  });

  it("rejects scalar mixed-rate combine and append", async () => {
    const combined = await tool.combine({ ...group(sample(rational)), entries: [
      ...group(sample(rational)).entries, ...group(sample(approximate)).entries,
    ] });
    expect(combined.success).toBe(false);
    expect(combined.diagnostics[0].code).toBe("FrameRate.Invalid");
    expect((await tool.append(group(sample(rational)), group(sample(approximate)))).success).toBe(false);
    const same = await tool.combine(group(sample(rational, "0")));
    expect(same.success).toBe(true);
    expect(same.chapterSet!.durationTicks).toBe("0");
  });

  it("rejects a combine when an entry duration is unknown", async () => {
    const unknownDuration = await tool.combine({ ...group(sample(rational)), entries: [
      ...group(sample(rational)).entries, ...group(sample(rational, null)).entries,
    ] });
    expect(unknownDuration.success).toBe(false);
    expect(unknownDuration.diagnostics[0].code).toBe("Structure.Invalid");

    const appended = await tool.append(group(sample(rational)), group(sample(rational, null)));
    expect(appended.success).toBe(false);
    expect(appended.diagnostics[0].code).toBe("Structure.Invalid");
  });
});
