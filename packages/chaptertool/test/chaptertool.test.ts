import { describe, expect, it } from "vitest";

import { ChapterTool, MAX_INPUT_BYTES } from "../src/index.ts";
import type { ExactChapterSet, ExactFrameRate } from "../src/exact-types.ts";

const chapterText = `CHAPTER01=00:00:00.000
CHAPTER01NAME=Opening
CHAPTER02=00:01:00.000
CHAPTER02NAME=Middle
`;
const rate: ExactFrameRate = { kind: "rational", numerator: "24", denominator: "1" };

describe("ChapterTool package entry point", () => {
  it("imports UTF-8 text and serializes XML", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "sample.txt" });

    expect(imported.success).toBe(true);
    const chapterSet = imported.groups[0].entries[0].chapterSet;
    expect(chapterSet.chapters).toHaveLength(2);
    expect(chapterSet.chapters.map(({ name }) => name)).toEqual(["Opening", "Middle"]);

    const exported = await tool.serialize(chapterSet, { format: "xml" });
    expect(exported.success).toBe(true);
    expect(exported.fileExtension).toBe(".xml");
    expect(exported.content).toMatch(/<Chapters>/);
  });

  it("applies an expression explicitly before serialization", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "expression.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;
    const before = chapterSet.chapters[0].startTicks;

    const applied = await tool.applyExpression(chapterSet, true, "t + 1", rate);
    expect(applied.success).toBe(true);
    expect(applied.chapterSet!.chapters[0].startTicks).toBe("10000000");
    expect(chapterSet.chapters[0].startTicks).toBe(before);

    const exported = await tool.serialize(applied.chapterSet!, { format: "timecodes" });
    expect(exported.success).toBe(true);
    expect(exported.content).toContain("00:00:01.000");
  });

  it("imports Buffer and Uint8Array content", async () => {
    const tool = new ChapterTool();
    const bufferResult = await tool.import(Buffer.from(chapterText), { fileName: "buffer.txt" });
    const bytesResult = await tool.import(new TextEncoder().encode(chapterText), { fileName: "bytes.txt" });

    expect(bufferResult.groups[0].entries[0].chapterSet.chapters).toHaveLength(2);
    expect(bytesResult.groups[0].entries[0].chapterSet.chapters).toHaveLength(2);
  });

  it("exposes Core format and import catalogs", async () => {
    const tool = new ChapterTool();
    const formats = await tool.formats();

    expect(formats.map(({ code }) => code)).toEqual(["txt", "xml", "qpf", "timecodes", "tsmuxer", "cue", "json", "vtt", "celltimes"]);
    expect((await tool.importFormats()).find(({ code }) => code === "hddvd-xpl")?.displayName).toBe("HD-DVD XPL");
    expect((await tool.xmlLanguages()).some(({ code }) => code === "und")).toBe(true);
    expect((await tool.outputEncodings()).some(({ id }) => id === "utf8")).toBe(true);
    expect(await tool.isBinaryExtension(".mpls")).toBe(true);
    expect(await tool.isBinaryExtension(".xpl")).toBe(false);
  });

  it("rejects unsupported JavaScript input", async () => {
    const tool = new ChapterTool();

    await expect(tool.import(42 as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "Chapter content must be a string, Buffer, or Uint8Array.",
    });
  });

  it("rejects input above the shared byte limit before the WASM boundary", async () => {
    const tool = new ChapterTool();

    await expect(tool.maxInputBytes()).resolves.toBe(MAX_INPUT_BYTES);
    await expect(tool.import("a".repeat(MAX_INPUT_BYTES + 1))).rejects.toMatchObject({
      name: "RangeError",
      code: "INPUT_TOO_LARGE",
      maxBytes: MAX_INPUT_BYTES,
      actualBytes: MAX_INPUT_BYTES + 1,
    });
  });

  it("preserves Unicode chapter names through the JSON boundary", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "unicode.txt" });

    const result = await tool.rename(imported.groups[0].entries[0].chapterSet, 0, "第一章");

    expect(result.chapterSet!.chapters[0].name).toBe("第一章");
  });

  it("reports unsupported serialization formats", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "empty.txt" });

    const result = await tool.serialize(imported.groups[0].entries[0].chapterSet, { format: "" });

    expect(result.success).toBe(false);
    expect(result.diagnostics).toHaveLength(1);
  });

  it("keeps exact frame rates and time ticks as strings", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "frames.txt" });
    const set: ExactChapterSet = {
      ...imported.groups[0].entries[0].chapterSet,
      frameRate: rate,
    };
    const framed = await tool.updateFrames(set, rate);

    expect(framed.chapterSet!.frameRate).toEqual(rate);
    expect(framed.chapterSet!.chapters[1].framesInfo).toBe("1440");
    expect(framed.chapterSet!.chapters[1].startTicks).toBe("600000000");
  });
});
