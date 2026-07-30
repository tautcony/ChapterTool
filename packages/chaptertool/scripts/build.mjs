import { existsSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { execFileSync } from "node:child_process";
import fsExtra from "fs-extra";
import { build as bundle } from "tsdown";

import {
  inspectBuildEnvironment,
  reportBuildEnvironment,
  resolveBuildPaths,
} from "./check-environment.mjs";

const paths = resolveBuildPaths();
const {
  repositoryDirectory,
  projectPath,
  publishDirectory,
  sourceDirectory,
  distributionDirectory,
} = paths;
const runtimeDirectory = join(distributionDirectory, "runtime");
const frameworkSourceDirectory = join(publishDirectory, "wwwroot", "_framework");
const nodeProjectBinDirectory = join(
  repositoryDirectory,
  "src",
  "ChapterTool.Node",
  "bin",
  "Release",
);
const frameworkDirectory = join(runtimeDirectory, "_framework");
const { copy, pathExists, readdir, remove } = fsExtra;

const environment = inspectBuildEnvironment(paths);
reportBuildEnvironment(environment);

const publishArguments = [
  "publish",
  projectPath,
  "--configuration",
  "Release",
  "--output",
  publishDirectory,
];

if (!environment.hasWasmTools) {
  publishArguments.push(
    "-p:WasmBuildNative=false",
    "-p:WasmRunWasmOpt=false",
  );
}

// Clean publish output first — dotnet publish does not clear the output directory,
// so stale hashed WASM files from previous builds would accumulate.
await remove(publishDirectory);
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
      neverBundle: (id) => id.includes("/runtime/") || id.startsWith("./runtime/"),
    },
  },
});

// Clean runtime directory first to avoid stale hashed WASM files from previous builds.
await remove(runtimeDirectory);
await copy(frameworkSourceDirectory, frameworkDirectory, {
  filter: (source) => !source.endsWith(".br") && !source.endsWith(".gz"),
});

// Publish omits the runtime JS source maps, but the emitted files still reference them.
// Resolve the TFM directory dynamically at this point — dotnet publish has already run,
// so the intermediate bin directory is guaranteed to exist.
// Copy the maps from the intermediate build output so loaders can resolve sourceMappingURL.
const frameworkBuildDirectory = (() => {
  if (!existsSync(nodeProjectBinDirectory)) {
    return undefined;
  }
  const tfmDir = readdirSync(nodeProjectBinDirectory).find((entry) => entry.startsWith("net"));
  return tfmDir ? join(nodeProjectBinDirectory, tfmDir, "wwwroot", "_framework") : undefined;
})();
if (frameworkBuildDirectory && await pathExists(frameworkBuildDirectory)) {
  for (const entry of await readdir(frameworkBuildDirectory)) {
    if (!entry.endsWith(".map")) {
      continue;
    }

    await copy(join(frameworkBuildDirectory, entry), join(frameworkDirectory, entry));
  }
}

console.log(`ChapterTool Node package written to ${distributionDirectory}`);
