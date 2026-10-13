import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

import { inspectRepositoryLayout, resolveBuildPaths } from "./check-environment.mjs";

const paths = resolveBuildPaths();
const { packageDirectory, packageOutputDirectory } = paths;
const npmCliPath = process.env.npm_execpath;

if (!npmCliPath) {
  throw new Error("npm_execpath is required to verify the package.");
}

inspectRepositoryLayout(paths);

rmSync(packageOutputDirectory, { recursive: true, force: true });
mkdirSync(packageOutputDirectory, { recursive: true });

const packOutput = execFileSync(
  process.execPath,
  [npmCliPath, "pack", "--ignore-scripts", "--json", "--pack-destination", packageOutputDirectory],
  { cwd: packageDirectory, encoding: "utf8" },
).trim();

let packResult;
try {
  const jsonStart = packOutput.lastIndexOf("\n[");
  const jsonText = packOutput.slice(jsonStart >= 0 ? jsonStart + 1 : packOutput.indexOf("["));
  packResult = JSON.parse(jsonText);
} catch (error) {
  throw new Error(`npm pack did not return JSON: ${error instanceof Error ? error.message : String(error)}`, { cause: error });
}

const archiveName = packResult[0]?.filename;
if (typeof archiveName !== "string" || archiveName.length === 0) {
  throw new Error("npm pack did not produce a package archive.");
}

const archivePath = resolve(packageOutputDirectory, archiveName);
const packedFiles = new Set((packResult[0].files ?? []).map(({ path }) => path));
for (const requiredFile of [
  "dist/index.mjs",
  "dist/index.d.ts",
  "dist/runtime/_framework/dotnet.js",
  "dist/runtime/_framework/dotnet.runtime.js.map",
]) {
  if (!packedFiles.has(requiredFile)) {
    throw new Error(`The package archive does not contain ${requiredFile}.`);
  }
}

const consumerDirectory = mkdtempSync(join(tmpdir(), "chaptertool-node-consumer-"));
try {
  writeFileSync(
    join(consumerDirectory, "package.json"),
    JSON.stringify({ name: "chaptertool-node-package-check", private: true, type: "module" }),
  );

  execFileSync(
    process.execPath,
    [npmCliPath, "install", "--ignore-scripts", "--no-audit", "--no-fund", archivePath],
    { cwd: consumerDirectory, stdio: "inherit" },
  );

  const checkScript = `
    import { ChapterTool } from "@chaptertool/node";

    const result = await new ChapterTool().import(
      "CHAPTER01=00:00:00.000\\nCHAPTER01NAME=Package check\\n",
      { fileName: "package-check.txt" }
    );
    if (!result.success || result.groups[0]?.entries[0]?.chapterSet.chapters.length !== 1) {
      throw new Error("The installed npm package did not execute the Core import API.");
    }
    const tool = new ChapterTool();
    const exact = await tool.import("CHAPTER01=00:00:00.000\\nCHAPTER01NAME=Exact\\n");
    const content = exact.groups[0].entries[0].chapterSet;
    content.chapters[0].startTicks = "9007199254740993";
    content.frameRate = { kind: "rational", numerator: "48000", denominator: "2002" };
    content.durationTicks = null;
    const renamed = await tool.rename(content, 0, "Packed");
    if (!renamed.success || renamed.chapterSet.chapters[0].startTicks !== "9007199254740993"
        || renamed.chapterSet.frameRate.numerator !== "24000" || renamed.chapterSet.durationTicks !== null) {
      throw new Error("The installed exact facade lost ticks, authority, or duration availability.");
    }
    const converted = await tool.changeFrameRate(renamed.chapterSet, renamed.chapterSet.frameRate,
      { kind: "approximate", value: "25.0" });
    const exported = await tool.serialize(converted.chapterSet, { format: "qpf" });
    if (!converted.success || converted.chapterSet.frameRate.kind !== "approximate" || !exported.success) {
      throw new Error("The installed exact facade failed conversion and serialization.");
    }
  `;
  execFileSync(process.execPath, ["--input-type=module", "--eval", checkScript], {
    cwd: consumerDirectory,
    stdio: "inherit",
  });
  writeFileSync(join(consumerDirectory, "consumer.ts"), `
    import { ChapterTool, type ExactChapterSet, type ExactFrameRate } from "@chaptertool/node";
    const rate: ExactFrameRate = { kind: "rational", numerator: "24000", denominator: "1001" };
    const content: ExactChapterSet = { title: "Types", sourceName: null, importFormat: "Ogm",
      frameRate: rate, durationTicks: null, chapters: [{ displayNumber: 1, startTicks: "9007199254740993",
      endTicks: null, name: "A", framesInfo: "", frameAccuracy: "Neutral", kind: "Marker" }] };
    const result = await new ChapterTool().editFrame(content, 0, "1.5", rate);
    if (result.chapterSet) result.chapterSet.durationTicks satisfies string | null;
  `);
  execFileSync(process.execPath, [join(packageDirectory, "node_modules/typescript/bin/tsc"),
    "--noEmit", "--strict", "--skipLibCheck", "--module", "NodeNext", "--moduleResolution", "NodeNext",
    "--target", "ES2022", "--typeRoots", join(packageDirectory, "node_modules/@types"), "consumer.ts"],
  { cwd: consumerDirectory, stdio: "inherit" });
} finally {
  rmSync(consumerDirectory, { recursive: true, force: true });
}

const packageJson = JSON.parse(readFileSync(join(packageDirectory, "package.json"), "utf8"));
console.log(`Verified ${packageJson.name}@${packageJson.version} from ${archivePath}`);
