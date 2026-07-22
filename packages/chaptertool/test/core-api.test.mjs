import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { test } from "node:test";

import { createChapterTool } from "@chaptertool/node";

const chapterText = `CHAPTER01=00:00:00.000
CHAPTER01NAME=Opening
CHAPTER02=00:01:00.000
CHAPTER02NAME=Middle
`;

async function sample(tool) {
  const imported = await tool.import(chapterText, { fileName: "core-api.txt" });
  return imported.groups[0].entries[0].chapterSet;
}

function mplsGroup(id, durationSeconds) {
  return {
    sourcePath: `${id}.mpls`,
    defaultEntryIndex: 0,
    entries: [{
      id,
      displayName: id,
      canCombine: true,
      referencedMediaFiles: null,
      chapterSet: {
        title: id,
        sourceName: `${id}.m2ts`,
        importFormat: "Mpls",
        framesPerSecond: 24,
        durationSeconds,
        chapters: [{
          displayNumber: 1,
          startTimeSeconds: 0,
          name: id,
          framesInfo: "",
          endTimeSeconds: null,
          frameAccuracy: "Neutral",
          kind: "Marker"
        }]
      }
    }]
  };
}

test("exposes only the portable Core API surface", async () => {
  const tool = await createChapterTool();

  assert.deepEqual(Object.keys(tool).sort(), [
    "analyzeExpression",
    "append",
    "applyExpression",
    "applyOrderShift",
    "applyTemplate",
    "changeFrameRate",
    "chapterTextToQpfile",
    "combine",
    "createZones",
    "delete",
    "detectFrameRate",
    "editFrame",
    "editTime",
    "export",
    "expressionPresets",
    "expressionSymbols",
    "findFrameRate",
    "formatCueTime",
    "formatTime",
    "formats",
    "frameRates",
    "import",
    "importFormats",
    "insertBefore",
    "isBinaryExtension",
    "outputEncodings",
    "parseTime",
    "parseTimeOrZero",
    "project",
    "rename",
    "shiftFramesForward",
    "toCelltimes",
    "updateFrames",
    "xmlLanguages"
  ]);
});

test("exposes Core editing and segment operations", async () => {
  const tool = await createChapterTool();
  const chapterSet = await sample(tool);

  assert.equal((await tool.editTime(chapterSet, 1, "00:00:05.000")).chapterSet.chapters[1].startTimeSeconds, 5);
  assert.equal((await tool.editFrame(chapterSet, 1, "48", 24)).chapterSet.chapters[1].startTimeSeconds, 2);
  assert.equal((await tool.rename(chapterSet, 0, "Renamed")).chapterSet.chapters[0].name, "Renamed");
  assert.equal((await tool.delete(chapterSet, [1])).chapterSet.chapters.length, 1);
  assert.equal((await tool.insertBefore(chapterSet, 1)).chapterSet.chapters.length, 3);
  assert.equal((await tool.applyOrderShift(chapterSet, 2)).chapterSet.chapters[0].displayNumber, 3);
  assert.equal((await tool.applyTemplate(chapterSet, "Alpha\nBeta")).chapterSet.chapters[1].name, "Beta");
  const shifted = await tool.shiftFramesForward(chapterSet, 24, 24);
  assert.equal(shifted.chapterSet.chapters.length, 1);
  assert.equal(shifted.chapterSet.chapters[0].startTimeSeconds, 59);
  assert.match((await tool.createZones(chapterSet, [0, 1], 24)).zones, /0,/);

  const first = mplsGroup("first", 10);
  const second = mplsGroup("second", 20);
  assert.equal((await tool.combine(first)).chapterSet.chapters.length, 1);
  assert.equal((await tool.append(first, second)).chapterSet.chapters.length, 2);
});

test("exposes Core frame and expression operations", async () => {
  const tool = await createChapterTool();
  const chapterSet = await sample(tool);

  const frameRates = await tool.frameRates();
  assert.equal(frameRates.length, 8);
  assert.equal((await tool.findFrameRate(24)).code, "Fps24");
  assert.equal((await tool.detectFrameRate(chapterSet)).evaluatedChapterCount, 2);

  const framed = await tool.updateFrames(chapterSet, { optionCode: "Fps24" });
  assert.equal(framed.framesPerSecond, 24);
  assert.equal(framed.chapters[1].framesInfo, "1440");

  const changed = await tool.changeFrameRate(framed.chapterSet, 24, 25);
  assert.equal(changed.success, true);
  assert.equal(changed.chapterSet.framesPerSecond, 25);

  const expressed = await tool.applyExpression(framed.chapterSet, "t + 1");
  assert.equal(expressed.chapterSet.chapters[0].startTimeSeconds, 1);

  const analysis = await tool.analyzeExpression("t + ", { caretIndex: 4 });
  assert.ok(analysis.diagnostics.length > 0);
  assert.ok((await tool.expressionSymbols()).some(({ text }) => text === "t"));
  assert.ok((await tool.expressionPresets()).length > 0);

  const projected = await tool.project(chapterSet, { format: "txt", autoGenerateNames: true });
  assert.equal(projected.outputChapters[0].name, "Chapter 01");
});

test("exposes Core time, conversion, and metadata operations", async () => {
  const tool = await createChapterTool();
  const chapterSet = await sample(tool);

  assert.equal((await tool.parseTime("00:00:01.500")).seconds, 1.5);
  assert.equal(await tool.parseTimeOrZero("invalid"), 0);
  assert.equal(await tool.formatTime(1.5), "00:00:01.500");
  assert.equal(await tool.formatCueTime(60), "01:00:00");

  const celltimes = await tool.toCelltimes(chapterSet, 24);
  assert.equal(celltimes.success, true);
  assert.match(celltimes.content, /1440/);

  const qpfile = await tool.chapterTextToQpfile(chapterText, 24);
  assert.equal(qpfile.success, true);
  assert.match(qpfile.content, /1440 I/);

  assert.ok((await tool.xmlLanguages()).some(({ code }) => code === "und"));
  assert.ok((await tool.outputEncodings()).some(({ id }) => id === "utf8"));
  const importFormats = await tool.importFormats();
  assert.equal(importFormats.find(({ code }) => code === "hddvd-xpl").displayName, "HD-DVD XPL");
  assert.equal(importFormats.some(({ code }) => code === "media" || code === "bdmv"), false);
  assert.equal(await tool.isBinaryExtension(".mpls"), true);
  assert.equal(await tool.isBinaryExtension(".xpl"), false);
});

test("imports the pure Core XPL format", async () => {
  const tool = await createChapterTool();
  const content = await readFile(new URL("../../../tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Xpl/VPLST001.XPL", import.meta.url));

  const imported = await tool.import(content, { fileName: "VPLST001.XPL" });

  assert.equal(imported.success, true);
  assert.equal(imported.groups[0].entries[0].chapterSet.importFormat, "HdDvdXpl");
  assert.equal(imported.groups[0].entries[0].chapterSet.chapters.length, 29);
});
