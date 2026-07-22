import { afterEach, describe, expect, it, vi } from "vitest";

import { reportBuildEnvironment } from "../scripts/check-environment.mjs";

describe("build environment reporting", () => {
afterEach(() => {
  vi.restoreAllMocks();
});

it("reports guidance when wasm-tools is missing", () => {
  const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

  reportBuildEnvironment({
    sdkVersion: "10.0.300",
    hasWasmTools: false,
    workloadCheckError: undefined
  });

  expect(warn).toHaveBeenCalledTimes(2);
  expect(warn.mock.calls[0][0]).toMatch(/unoptimized WASM runtime/);
  expect(warn.mock.calls[1][0]).toBe("Install it with: dotnet workload install wasm-tools");
});
});
