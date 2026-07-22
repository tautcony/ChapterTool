import assert from "node:assert/strict";
import { test } from "node:test";

import {
  createChapterTool,
  exportChapters,
  importChapters
} from "@chaptertool/node";

const chapterText = `CHAPTER01=00:00:00.000
CHAPTER01NAME=Opening
CHAPTER02=00:01:00.000
CHAPTER02NAME=Middle
`;

test("imports UTF-8 text and exports XML", async () => {
  const imported = await importChapters(chapterText, { fileName: "sample.txt" });

  assert.equal(imported.success, true);
  const chapterSet = imported.groups[0].entries[0].chapterSet;
  assert.equal(chapterSet.chapters.length, 2);
  assert.deepEqual(chapterSet.chapters.map(({ name }) => name), ["Opening", "Middle"]);

  const exported = await exportChapters(chapterSet, { format: "xml" });
  assert.equal(exported.success, true);
  assert.equal(exported.fileExtension, ".xml");
  assert.match(exported.content, /<Chapters>/);
});

test("imports Buffer and Uint8Array content", async () => {
  const tool = await createChapterTool();
  const bufferResult = await tool.import(Buffer.from(chapterText), { fileName: "buffer.txt" });
  const bytesResult = await tool.import(new TextEncoder().encode(chapterText), { fileName: "bytes.txt" });

  assert.equal(bufferResult.groups[0].entries[0].chapterSet.chapters.length, 2);
  assert.equal(bytesResult.groups[0].entries[0].chapterSet.chapters.length, 2);
});

test("lists and exports every Core format code", async () => {
  const tool = await createChapterTool();
  const formats = await tool.formats();

  assert.deepEqual(
    formats.map(({ code }) => code),
    ["txt", "xml", "qpf", "timecodes", "tsmuxer", "cue", "json", "vtt", "celltimes"]
  );

  const imported = await tool.import(chapterText, { fileName: "formats.txt" });
  const chapterSet = imported.groups[0].entries[0].chapterSet;
  chapterSet.framesPerSecond = 24;

  for (const { code } of formats) {
    const exported = await tool.export(chapterSet, {
      format: code,
      sourceFileName: "source.wav"
    });
    assert.equal(exported.success, true, `${code}: ${JSON.stringify(exported.diagnostics)}`);
  }
});

test("rejects unsupported JavaScript input", async () => {
  const tool = await createChapterTool();

  await assert.rejects(tool.import(42), {
    name: "TypeError",
    message: "Chapter content must be a string, Buffer, or Uint8Array."
  });
});
