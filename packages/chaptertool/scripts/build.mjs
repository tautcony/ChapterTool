import { cpSync, mkdirSync, readdirSync, rmSync } from "node:fs";
import { join, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { inspectBuildEnvironment, reportBuildEnvironment } from "./check-environment.mjs";

const packageDirectory = fileURLToPath(new URL("..", import.meta.url));
const repositoryDirectory = resolve(packageDirectory, "../..");
const projectPath = join(repositoryDirectory, "src", "ChapterTool.Node", "ChapterTool.Node.csproj");
const publishDirectory = join(repositoryDirectory, "artifacts", "node-package-runtime");
const sourceDirectory = join(packageDirectory, "src");
const distributionDirectory = join(packageDirectory, "dist");
const legacyRuntimeDirectory = join(packageDirectory, "runtime");
const runtimeDirectory = join(distributionDirectory, "runtime");
const frameworkSourceDirectory = join(publishDirectory, "wwwroot", "_framework");
const frameworkDirectory = join(runtimeDirectory, "_framework");

const environment = inspectBuildEnvironment();
reportBuildEnvironment(environment);

rmSync(publishDirectory, { recursive: true, force: true });
rmSync(distributionDirectory, { recursive: true, force: true });
rmSync(legacyRuntimeDirectory, { recursive: true, force: true });
mkdirSync(distributionDirectory, { recursive: true });

const publishArguments = [
  "publish",
  projectPath,
  "--configuration",
  "Release",
  "--output",
  publishDirectory
];

if (!environment.hasWasmTools) {
  publishArguments.push(
    "-p:WasmBuildNative=false",
    "-p:WasmRunWasmOpt=false"
  );
}

execFileSync("dotnet", publishArguments, { cwd: repositoryDirectory, stdio: "inherit" });

cpSync(join(sourceDirectory, "index.mjs"), join(distributionDirectory, "index.mjs"));
cpSync(join(sourceDirectory, "index.d.ts"), join(distributionDirectory, "index.d.ts"));
mkdirSync(frameworkDirectory, { recursive: true });
cpSync(frameworkSourceDirectory, frameworkDirectory, { recursive: true });

for (const fileName of readdirSync(frameworkDirectory)) {
  if (fileName.endsWith(".br") || fileName.endsWith(".gz")) {
    rmSync(join(frameworkDirectory, fileName));
  }
}

console.log(`ChapterTool Node package written to ${distributionDirectory}`);
