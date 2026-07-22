import { join } from "node:path";
import { execFileSync } from "node:child_process";
import fsExtra from "fs-extra";
import { build as bundle } from "tsdown";

import {
  inspectBuildEnvironment,
  reportBuildEnvironment,
  resolveBuildPaths
} from "./check-environment.mjs";

const paths = resolveBuildPaths();
const {
  repositoryDirectory,
  projectPath,
  publishDirectory,
  sourceDirectory,
  distributionDirectory
} = paths;
const runtimeDirectory = join(distributionDirectory, "runtime");
const frameworkSourceDirectory = join(publishDirectory, "wwwroot", "_framework");
const frameworkDirectory = join(runtimeDirectory, "_framework");
const { copy } = fsExtra;

const environment = inspectBuildEnvironment(paths);
reportBuildEnvironment(environment);

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

await bundle({
  entry: [join(sourceDirectory, "index.ts")],
  format: "esm",
  outDir: distributionDirectory,
  target: "es2022",
  platform: "node",
  fixedExtension: true,
  outExtensions: () => ({ js: ".mjs", dts: ".d.ts" }),
  dts: true,
  sourcemap: true,
  treeshake: true,
  clean: true,
  deps: {
    neverBundle: (id) => id.includes("/runtime/") || id.startsWith("./runtime/"),
    dts: {
      neverBundle: (id) => id.includes("/runtime/") || id.startsWith("./runtime/")
    }
  }
});

await copy(frameworkSourceDirectory, frameworkDirectory, {
  filter: (source) => !source.endsWith(".br") && !source.endsWith(".gz")
});

console.log(`ChapterTool Node package written to ${distributionDirectory}`);
