import { describe, expect, it } from "vitest";

import { ChapterTool } from "../src/index.ts";
import type { ExactFrameRate } from "../src/exact-types.ts";

const chapterText = "CHAPTER01=00:00:00.000\nCHAPTER01NAME=Opening\n";
const rate: ExactFrameRate = { kind: "rational", numerator: "24", denominator: "1" };

describe("ChapterTool exact Core API mapping", () => {
  it("uses the exact API as the package entry point", () => {
    const tool = new ChapterTool();

    expect(tool).toBeInstanceOf(ChapterTool);
    expect("exact" in tool).toBe(false);
    expect("export" in tool).toBe(false);
    expect(typeof tool.serialize).toBe("function");
    expect(typeof tool.applyExpression).toBe("function");
  });

  it("serializes transformed snapshots without applying the transform again", async () => {
    const tool = new ChapterTool();
    const imported = await tool.import(chapterText, { fileName: "core-api.txt" });
    const chapterSet = imported.groups[0].entries[0].chapterSet;
    const originalTicks = chapterSet.chapters[0].startTicks;

    const transformed = await tool.applyExpression(chapterSet, true, "t + 1", rate);
    const serialized = await tool.serialize(transformed.chapterSet!, { format: "timecodes" });
    const repeated = await tool.serialize(transformed.chapterSet!, { format: "timecodes" });

    expect(serialized.success).toBe(true);
    expect(serialized.content).toBe("00:00:01.000");
    expect(repeated.content).toBe(serialized.content);
    expect(chapterSet.chapters[0].startTicks).toBe(originalTicks);
    expect(transformed.chapterSet!.chapters[0].startTicks).toBe("10000000");
  });
});
