import { describe, expect, it } from "vitest";

import { ChapterTool, MAX_INPUT_BYTES } from "../src/index.ts";
import { encodeJson, decodeJson } from "../src/utils/json.ts";
import { requireBoolean, requireInteger, requireIndex, requireNumber } from "../src/utils/validation.ts";

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

  it("preserves the applyExpression export option across the JSON boundary", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "expression.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;
    chapterSet.framesPerSecond = 24;

    const exported = await tool.export(chapterSet, {
      format: "timecodes",
      applyExpression: true,
      expression: "t + 1",
    });

    expect(exported.success).toBe(true);
    expect(exported.content).toBe("00:00:01.000\n00:01:01.000");
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
        sourceFileName: "source.wav",
      });
      expect(exported.success, `${code}: ${JSON.stringify(exported.diagnostics)}`).toBe(true);
    }
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

  it("rejects invalid dynamic options before the Core boundary", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "validation.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;

    await expect(tool.import(chapterText, null as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "options must be an object.",
    });
    await expect(tool.updateFrames(chapterSet, null as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "options must be an object.",
    });
    await expect(tool.rename(chapterSet, 0, undefined as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "name must be a string.",
    });
    await expect(tool.delete(chapterSet, [0, -1])).rejects.toMatchObject({
      name: "TypeError",
      message: "indexes must be an array of non-negative chapter indexes.",
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

  it("rejects export and project with an empty format code", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "empty.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;

    await expect(tool.export(chapterSet, { format: "" })).rejects.toMatchObject({
      name: "TypeError",
      message: "options.format must be an export format code, such as 'xml'.",
    });
    await expect(tool.project(chapterSet, { format: "" })).rejects.toMatchObject({
      name: "TypeError",
      message: "options.format must be an export format code, such as 'xml'.",
    });
  });

  it("rejects updateFrames with a non-string optionCode", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "frames.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;

    await expect(tool.updateFrames(chapterSet, { optionCode: 123 } as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "options.optionCode must be a string.",
    });
  });

  it("validates timecodeText when passed to chapterTextToQpfile", async () => {
    const tool = new ChapterTool();
    // The JS‑side requireString should be exercised even when the .NET parser
    // rejects the timecode format — a TypeError on the JS side proves the path.
    await expect(tool.chapterTextToQpfile(chapterText, 24, 123 as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "timecodeText must be a string.",
    });
  });

  it("rejects a blank fileName", async () => {
    const tool = new ChapterTool();
    await expect(tool.import(chapterText, { fileName: "  " })).rejects.toMatchObject({
      name: "TypeError",
      message: "fileName must be a non-empty string.",
    });
  });

  it("rejects invalid validation primitives before the Core boundary", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "primitives.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;

    await expect(tool.updateFrames(chapterSet, { round: "yes" } as any)).rejects.toMatchObject({
      name: "TypeError",
      message: "options.round must be a boolean.",
    });
    await expect(tool.shiftFramesForward(chapterSet, 24.5, 24)).rejects.toMatchObject({
      name: "TypeError",
      message: "frames must be a safe integer.",
    });
    await expect(tool.changeFrameRate(chapterSet, NaN, 24)).rejects.toMatchObject({
      name: "TypeError",
      message: "sourceFps must be a finite number.",
    });
    await expect(tool.editTime(chapterSet, -1, "00:00:05.000")).rejects.toMatchObject({
      name: "RangeError",
      message: "index must be a non-negative integer.",
    });
  });
});

describe("JSON encoding utilities", () => {
  it("rejects values that JSON.stringify cannot serialise", () => {
    const circular: Record<string, unknown> = {};
    circular.self = circular;
    expect(() => encodeJson(circular, "circular")).toThrow(TypeError);
    expect(() => encodeJson(circular, "circular")).toThrow("circular must be serializable as JSON.");
  });

  it("rejects values that JSON.stringify returns undefined for", () => {
    expect(() => encodeJson(undefined, "value")).toThrow(TypeError);
    expect(() => encodeJson(undefined, "value")).toThrow("value must be serializable as JSON.");
  });

  it("rejects invalid JSON text", () => {
    expect(() => decodeJson("not json", "TestOp")).toThrow(Error);
    expect(() => decodeJson("not json", "TestOp")).toThrow("ChapterTool TestOp returned invalid JSON.");
  });
});

describe("validation primitives", () => {
  it("requireBoolean rejects non-boolean values", () => {
    expect(() => requireBoolean("true", "flag")).toThrow(TypeError);
    expect(() => requireBoolean("true", "flag")).toThrow("flag must be a boolean.");
  });

  it("requireInteger rejects non-safe-integer values", () => {
    expect(() => requireInteger(3.14, "count")).toThrow(TypeError);
    expect(() => requireInteger(3.14, "count")).toThrow("count must be a safe integer.");
  });

  it("requireIndex rejects negative values", () => {
    expect(() => requireIndex(-1, "pos")).toThrow(RangeError);
    expect(() => requireIndex(-1, "pos")).toThrow("pos must be a non-negative integer.");
  });

  it("requireNumber rejects NaN and Infinity", () => {
    expect(() => requireNumber(NaN, "value")).toThrow(TypeError);
    expect(() => requireNumber(NaN, "value")).toThrow("value must be a finite number.");
    expect(() => requireNumber(Infinity, "value")).toThrow(TypeError);
  });
});
