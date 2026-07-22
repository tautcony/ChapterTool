import { defineConfig } from "vitest/config";

export default defineConfig({
  server: {
    sourcemapIgnoreList: (sourcePath) => sourcePath.includes("/dist/runtime/")
  },
  test: {
    include: ["test/**/*.test.mjs"],
    fileParallelism: false,
    maxWorkers: 1,
    minWorkers: 1,
    pool: "forks",
    onConsoleLog(log) {
      if (log.includes("Failed to load source map") && log.includes("/dist/runtime/")) {
        return false;
      }
    }
  }
});
