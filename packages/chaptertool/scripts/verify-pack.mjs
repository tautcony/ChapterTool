import { execFileSync } from "node:child_process";
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packageDirectory = fileURLToPath(new URL("..", import.meta.url));
const repositoryDirectory = resolve(packageDirectory, "../..");
const packageOutputDirectory = join(repositoryDirectory, "artifacts", "npm");

rmSync(packageOutputDirectory, { recursive: true, force: true });
mkdirSync(packageOutputDirectory, { recursive: true });

const packOutput = execFileSync(
  "npm",
  ["pack", "--json", "--pack-destination", packageOutputDirectory],
  { cwd: packageDirectory, encoding: "utf8" }
).trim();

let packResult;
try {
  const jsonStart = packOutput.lastIndexOf("\n[");
  const jsonText = packOutput.slice(jsonStart >= 0 ? jsonStart + 1 : packOutput.indexOf("["));
  packResult = JSON.parse(jsonText);
} catch (error) {
  throw new Error(`npm pack did not return JSON: ${error instanceof Error ? error.message : String(error)}`);
}

const archiveName = packResult[0]?.filename;
if (typeof archiveName !== "string" || archiveName.length === 0) {
  throw new Error("npm pack did not produce a package archive.");
}

const archivePath = resolve(packageOutputDirectory, archiveName);
const packedFiles = new Set((packResult[0].files ?? []).map(({ path }) => path));
for (const requiredFile of ["dist/index.mjs", "dist/index.d.ts", "dist/runtime/_framework/dotnet.js"]) {
  if (!packedFiles.has(requiredFile)) {
    throw new Error(`The package archive does not contain ${requiredFile}.`);
  }
}

const consumerDirectory = mkdtempSync(join(tmpdir(), "chaptertool-node-consumer-"));
try {
  writeFileSync(
    join(consumerDirectory, "package.json"),
    JSON.stringify({ name: "chaptertool-node-package-check", private: true, type: "module" })
  );

  execFileSync(
    "npm",
    ["install", "--ignore-scripts", "--no-audit", "--no-fund", archivePath],
    { cwd: consumerDirectory, stdio: "inherit" }
  );

  const checkScript = `
    import { importChapters } from "@chaptertool/node";

    const result = await importChapters(
      "CHAPTER01=00:00:00.000\\nCHAPTER01NAME=Package check\\n",
      { fileName: "package-check.txt" }
    );
    if (!result.success || result.groups[0]?.entries[0]?.chapterSet.chapters.length !== 1) {
      throw new Error("The installed npm package did not execute the Core import API.");
    }
  `;
  execFileSync(process.execPath, ["--input-type=module", "--eval", checkScript], {
    cwd: consumerDirectory,
    stdio: "inherit"
  });
} finally {
  rmSync(consumerDirectory, { recursive: true, force: true });
}

const packageJson = JSON.parse(readFileSync(join(packageDirectory, "package.json"), "utf8"));
console.log(`Verified ${packageJson.name}@${packageJson.version} from ${archivePath}`);
