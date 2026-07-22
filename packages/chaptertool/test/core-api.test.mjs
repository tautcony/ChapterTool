import { readFile } from "node:fs/promises";
import { describe, expect, it } from "vitest";

import { ChapterTool } from "@chaptertool/node";

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

describe("ChapterTool Core API mapping", () => {
it("exposes only the portable Core API surface", async () => {
  const tool = new ChapterTool();

  expect(tool).toBeInstanceOf(ChapterTool);
  expect(Object.getOwnPropertyNames(Object.getPrototypeOf(tool)).filter((name) => name !== "constructor").sort()).toEqual([
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

it("exposes Core editing and segment operations", async () => {
  const tool = new ChapterTool();
  const chapterSet = await sample(tool);

  expect((await tool.editTime(chapterSet, 1, "00:00:05.000")).chapterSet.chapters[1].startTimeSeconds).toBe(5);
  expect((await tool.editFrame(chapterSet, 1, "48", 24)).chapterSet.chapters[1].startTimeSeconds).toBe(2);
  expect((await tool.rename(chapterSet, 0, "Renamed")).chapterSet.chapters[0].name).toBe("Renamed");
  expect((await tool.delete(chapterSet, [1])).chapterSet.chapters).toHaveLength(1);
  expect((await tool.insertBefore(chapterSet, 1)).chapterSet.chapters).toHaveLength(3);
  expect((await tool.applyOrderShift(chapterSet, 2)).chapterSet.chapters[0].displayNumber).toBe(3);
  expect((await tool.applyTemplate(chapterSet, "Alpha\nBeta")).chapterSet.chapters[1].name).toBe("Beta");
  const shifted = await tool.shiftFramesForward(chapterSet, 24, 24);
  expect(shifted.chapterSet.chapters).toHaveLength(1);
  expect(shifted.chapterSet.chapters[0].startTimeSeconds).toBe(59);
  expect((await tool.createZones(chapterSet, [0, 1], 24)).zones).toMatch(/0,/);

  const first = mplsGroup("first", 10);
  const second = mplsGroup("second", 20);
  expect((await tool.combine(first)).chapterSet.chapters).toHaveLength(1);
  expect((await tool.append(first, second)).chapterSet.chapters).toHaveLength(2);
});

it("exposes Core frame and expression operations", async () => {
  const tool = new ChapterTool();
  const chapterSet = await sample(tool);

  const frameRates = await tool.frameRates();
  expect(frameRates).toHaveLength(8);
  expect((await tool.findFrameRate(24)).code).toBe("Fps24");
  expect((await tool.detectFrameRate(chapterSet)).evaluatedChapterCount).toBe(2);

  const framed = await tool.updateFrames(chapterSet, { optionCode: "Fps24" });
  expect(framed.framesPerSecond).toBe(24);
  expect(framed.chapters[1].framesInfo).toBe("1440");

  const changed = await tool.changeFrameRate(framed.chapterSet, 24, 25);
  expect(changed.success).toBe(true);
  expect(changed.chapterSet.framesPerSecond).toBe(25);

  const expressed = await tool.applyExpression(framed.chapterSet, "t + 1");
  expect(expressed.chapterSet.chapters[0].startTimeSeconds).toBe(1);

  const analysis = await tool.analyzeExpression("t + ", { caretIndex: 4 });
  expect(analysis.diagnostics.length).toBeGreaterThan(0);
  expect((await tool.expressionSymbols()).some(({ text }) => text === "t")).toBe(true);
  expect((await tool.expressionPresets()).length).toBeGreaterThan(0);

  const projected = await tool.project(chapterSet, { format: "txt", autoGenerateNames: true });
  expect(projected.outputChapters[0].name).toBe("Chapter 01");
});

it("exposes Core time, conversion, and metadata operations", async () => {
  const tool = new ChapterTool();
  const chapterSet = await sample(tool);

  expect((await tool.parseTime("00:00:01.500")).seconds).toBe(1.5);
  expect(await tool.parseTimeOrZero("invalid")).toBe(0);
  expect(await tool.formatTime(1.5)).toBe("00:00:01.500");
  expect(await tool.formatCueTime(60)).toBe("01:00:00");

  const celltimes = await tool.toCelltimes(chapterSet, 24);
  expect(celltimes.success).toBe(true);
  expect(celltimes.content).toMatch(/1440/);

  const qpfile = await tool.chapterTextToQpfile(chapterText, 24);
  expect(qpfile.success).toBe(true);
  expect(qpfile.content).toMatch(/1440 I/);

  expect((await tool.xmlLanguages()).some(({ code }) => code === "und")).toBe(true);
  expect((await tool.outputEncodings()).some(({ id }) => id === "utf8")).toBe(true);
  const importFormats = await tool.importFormats();
  expect(importFormats.find(({ code }) => code === "hddvd-xpl").displayName).toBe("HD-DVD XPL");
  expect(importFormats.some(({ code }) => code === "media" || code === "bdmv")).toBe(false);
  expect(await tool.isBinaryExtension(".mpls")).toBe(true);
  expect(await tool.isBinaryExtension(".xpl")).toBe(false);
});

it("imports the pure Core XPL format", async () => {
  const tool = new ChapterTool();
  const content = await readFile(new URL("../../../tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Xpl/VPLST001.XPL", import.meta.url));

  const imported = await tool.import(content, { fileName: "VPLST001.XPL" });

  expect(imported.success).toBe(true);
  expect(imported.groups[0].entries[0].chapterSet.importFormat).toBe("HdDvdXpl");
  expect(imported.groups[0].entries[0].chapterSet.chapters).toHaveLength(29);
});
});
