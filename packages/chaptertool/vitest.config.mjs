import { resolve } from "node:path";
import { defineConfig } from "vitest/config";

const packageRoot = import.meta.dirname;

export default defineConfig({
  plugins: [
    {
      name: "resolve-dotnet-runtime",
      resolveId(source) {
        // src/index.ts imports ./runtime/_framework/dotnet.js
        // Redirect to the built dist copy so the WASM runtime loads.
        if (source === "./runtime/_framework/dotnet.js") {
          return resolve(packageRoot, "dist/runtime/_framework/dotnet.js");
        }
        return undefined;
      },
    },
  ],
  server: {
    sourcemapIgnoreList: (sourcePath) => sourcePath.includes("/dist/runtime/"),
  },
  test: {
    include: ["test/**/*.test.ts"],
    fileParallelism: false,
    maxWorkers: 1,
    minWorkers: 1,
    pool: "forks",
    onConsoleLog(log) {
      if (log.includes("Failed to load source map") && log.includes("/dist/runtime/")) {
        return false;
      }
    },
    coverage: {
      provider: "v8",
      include: ["src/**/*.ts"],
      exclude: ["src/runtime/**"],
    },
  },
});
