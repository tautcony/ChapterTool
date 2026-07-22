import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, realpathSync, statSync } from "node:fs";
import { dirname, isAbsolute, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const expectedPackageName = "@chaptertool/node";

function requireDirectory(path, description) {
  let stats;
  try {
    stats = statSync(path);
  } catch (error) {
    throw new Error(`${description} does not exist at ${path}: ${error instanceof Error ? error.message : String(error)}`);
  }

  if (!stats.isDirectory()) {
    throw new Error(`${description} is not a directory: ${path}`);
  }
}

function requireFile(path, description) {
  let stats;
  try {
    stats = statSync(path);
  } catch (error) {
    throw new Error(`${description} does not exist at ${path}: ${error instanceof Error ? error.message : String(error)}`);
  }

  if (!stats.isFile()) {
    throw new Error(`${description} is not a file: ${path}`);
  }
}

function isWithin(root, path) {
  const relativePath = relative(root, path);
  return relativePath === "" || (
    relativePath !== ".." &&
    !relativePath.startsWith(`..${sep}`) &&
    !isAbsolute(relativePath)
  );
}

function findExistingAncestor(path) {
  let currentPath = path;
  while (!existsSync(currentPath)) {
    const parentPath = dirname(currentPath);
    if (parentPath === currentPath) {
      throw new Error(`No existing ancestor was found for ${path}.`);
    }
    currentPath = parentPath;
  }
  return currentPath;
}

function requireOwnedPath(root, path, expectedRelativePath, description) {
  const actualRelativePath = relative(root, path);
  if (actualRelativePath !== expectedRelativePath) {
    throw new Error(
      `${description} must resolve to ${expectedRelativePath} under ${root}. Detected: ${path}`
    );
  }

  const realRoot = realpathSync(root);
  const existingAncestor = findExistingAncestor(path);
  const realAncestor = realpathSync(existingAncestor);
  if (!isWithin(realRoot, realAncestor)) {
    throw new Error(`${description} escapes its validated root through ${existingAncestor}.`);
  }

  if (existsSync(path)) {
    requireDirectory(path, description);
  }
}

export function resolveBuildPaths() {
  const packageDirectory = fileURLToPath(new URL("..", import.meta.url));
  const repositoryDirectory = resolve(packageDirectory, "../..");
  const sourceDirectory = join(packageDirectory, "src");

  return {
    packageDirectory,
    repositoryDirectory,
    solutionPath: join(repositoryDirectory, "ChapterTool.Avalonia.slnx"),
    projectPath: join(repositoryDirectory, "src", "ChapterTool.Node", "ChapterTool.Node.csproj"),
    packageJsonPath: join(packageDirectory, "package.json"),
    sourceDirectory,
    entryPointPath: join(sourceDirectory, "index.ts"),
    publishDirectory: join(repositoryDirectory, "artifacts", "node-package-runtime"),
    packageOutputDirectory: join(repositoryDirectory, "artifacts", "npm"),
    distributionDirectory: join(packageDirectory, "dist")
  };
}

export function inspectRepositoryLayout(paths) {
  requireDirectory(paths.repositoryDirectory, "Repository directory");
  requireOwnedPath(
    paths.repositoryDirectory,
    paths.packageDirectory,
    join("packages", "chaptertool"),
    "ChapterTool package directory"
  );
  requireDirectory(paths.sourceDirectory, "ChapterTool package source directory");
  requireFile(paths.solutionPath, "Avalonia solution");
  requireFile(paths.projectPath, "ChapterTool Node project");
  requireFile(paths.packageJsonPath, "ChapterTool package manifest");
  requireFile(paths.entryPointPath, "ChapterTool package entry point");

  let packageJson;
  try {
    packageJson = JSON.parse(readFileSync(paths.packageJsonPath, "utf8"));
  } catch (error) {
    throw new Error(
      `ChapterTool package manifest is not valid JSON: ${error instanceof Error ? error.message : String(error)}`
    );
  }
  if (packageJson.name !== expectedPackageName) {
    throw new Error(
      `ChapterTool package manifest must have name ${expectedPackageName}. Detected: ${String(packageJson.name)}`
    );
  }

  requireOwnedPath(
    paths.repositoryDirectory,
    paths.publishDirectory,
    join("artifacts", "node-package-runtime"),
    "Node runtime publish directory"
  );
  requireOwnedPath(
    paths.repositoryDirectory,
    paths.packageOutputDirectory,
    join("artifacts", "npm"),
    "npm package output directory"
  );
  requireOwnedPath(
    paths.packageDirectory,
    paths.distributionDirectory,
    "dist",
    "ChapterTool package distribution directory"
  );
}

function runDotnet(arguments_) {
  try {
    return execFileSync("dotnet", arguments_, {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"]
    }).trim();
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error("The .NET SDK is required, but the dotnet command was not found.");
    }

    const details = error?.stderr?.trim();
    throw new Error(details || `dotnet ${arguments_.join(" ")} failed.`);
  }
}

function parseWorkloadList(output) {
  const jsonStart = output.indexOf("{");
  if (jsonStart < 0) {
    throw new Error("The .NET workload command did not return machine-readable JSON.");
  }

  return JSON.parse(output.slice(jsonStart));
}

export function inspectBuildEnvironment(paths = resolveBuildPaths()) {
  inspectRepositoryLayout(paths);

  const sdkVersion = runDotnet(["--version"]);
  if (!sdkVersion.startsWith("10.")) {
    throw new Error(`The ChapterTool Node package requires the .NET 10 SDK. Detected: ${sdkVersion}`);
  }

  let hasWasmTools = false;
  let workloadCheckError;
  try {
    const workloadList = parseWorkloadList(runDotnet(["workload", "list", "--machine-readable"]));
    hasWasmTools = (workloadList.installed ?? []).some((workload) =>
      (typeof workload === "string" ? workload : workload.id) === "wasm-tools");
  } catch (error) {
    workloadCheckError = error instanceof Error ? error.message : String(error);
  }

  return { sdkVersion, hasWasmTools, workloadCheckError };
}

export function reportBuildEnvironment(environment) {
  console.log(`.NET SDK ${environment.sdkVersion}`);
  if (environment.hasWasmTools) {
    console.log("wasm-tools detected. Release WASM optimization is enabled.");
    return;
  }

  console.warn("Warning: wasm-tools was not detected. The package will use the unoptimized WASM runtime.");
  if (environment.workloadCheckError) {
    console.warn(`Workload check failed: ${environment.workloadCheckError}`);
  }
  console.warn("Install it with: dotnet workload install wasm-tools");
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  reportBuildEnvironment(inspectBuildEnvironment());
}
