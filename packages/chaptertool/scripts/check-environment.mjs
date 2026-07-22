import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

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

export function inspectBuildEnvironment() {
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
