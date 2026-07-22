import { describe, expect, it } from "vitest";

import { ChapterTool } from "@chaptertool/node";

const chapterText = `CHAPTER01=00:00:00.000
CHAPTER01NAME=Opening
CHAPTER02=00:01:00.000
CHAPTER02NAME=Middle
`;

describe("ChapterTool package entry point", () => {
it("imports UTF-8 text and exports XML", async () => {
  const tool = new ChapterTool();
  const imported = await tool.import(chapterText, { fileName: "sample.txt" });

  expect(imported.success).toBe(true);
  const chapterSet = imported.groups[0].entries[0].chapterSet;
  expect(chapterSet.chapters).toHaveLength(2);
  expect(chapterSet.chapters.map(({ name }) => name)).toEqual(["Opening", "Middle"]);

  const exported = await tool.export(chapterSet, { format: "xml" });
  expect(exported.success).toBe(true);
  expect(exported.fileExtension).toBe(".xml");
  expect(exported.content).toMatch(/<Chapters>/);
});

it("imports Buffer and Uint8Array content", async () => {
  const tool = new ChapterTool();
  const bufferResult = await tool.import(Buffer.from(chapterText), { fileName: "buffer.txt" });
  const bytesResult = await tool.import(new TextEncoder().encode(chapterText), { fileName: "bytes.txt" });

  expect(bufferResult.groups[0].entries[0].chapterSet.chapters).toHaveLength(2);
  expect(bytesResult.groups[0].entries[0].chapterSet.chapters).toHaveLength(2);
});

it("lists and exports every Core format code", async () => {
  const tool = new ChapterTool();
  const formats = await tool.formats();

  expect(
    formats.map(({ code }) => code),
  ).toEqual(["txt", "xml", "qpf", "timecodes", "tsmuxer", "cue", "json", "vtt", "celltimes"]);

  const imported = await tool.import(chapterText, { fileName: "formats.txt" });
  const chapterSet = imported.groups[0].entries[0].chapterSet;
  chapterSet.framesPerSecond = 24;

  for (const { code } of formats) {
    const exported = await tool.export(chapterSet, {
      format: code,
      sourceFileName: "source.wav"
    });
    expect(exported.success, `${code}: ${JSON.stringify(exported.diagnostics)}`).toBe(true);
  }
});

it("rejects unsupported JavaScript input", async () => {
  const tool = new ChapterTool();

  await expect(tool.import(42)).rejects.toMatchObject({
    name: "TypeError",
    message: "Chapter content must be a string, Buffer, or Uint8Array."
  });
});

it("rejects invalid dynamic options before the Core boundary", async () => {
  const tool = new ChapterTool();
  const imported = await tool.import(chapterText, { fileName: "validation.txt" });
  const chapterSet = imported.groups[0].entries[0].chapterSet;

  await expect(tool.import(chapterText, null)).rejects.toMatchObject({
    name: "TypeError",
    message: "options must be an object."
  });
  await expect(tool.updateFrames(chapterSet, null)).rejects.toMatchObject({
    name: "TypeError",
    message: "options must be an object."
  });
  await expect(tool.rename(chapterSet, 0, undefined)).rejects.toMatchObject({
    name: "TypeError",
    message: "name must be a string."
  });
  await expect(tool.delete(chapterSet, [0, -1])).rejects.toMatchObject({
    name: "TypeError",
    message: "indexes must be an array of non-negative chapter indexes."
  });

  expect(chapterSet.chapters[0].name).toBe("Opening");
});

it("preserves Unicode chapter names through the JSON boundary", async () => {
  const tool = new ChapterTool();
  const imported = await tool.import(chapterText, { fileName: "unicode.txt" });
  const chapterSet = imported.groups[0].entries[0].chapterSet;

  const result = await tool.rename(chapterSet, 0, "第一章");

  expect(result.chapterSet.chapters[0].name).toBe("第一章");
});
});
