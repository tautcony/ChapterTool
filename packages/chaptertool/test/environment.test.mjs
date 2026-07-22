import assert from "node:assert/strict";
import { test } from "node:test";

import { reportBuildEnvironment } from "../scripts/check-environment.mjs";

test("missing wasm-tools produces guidance without failing", () => {
  const warnings = [];
  const originalWarn = console.warn;
  console.warn = (message) => warnings.push(message);

  try {
    reportBuildEnvironment({
      sdkVersion: "10.0.300",
      hasWasmTools: false,
      workloadCheckError: undefined
    });
  } finally {
    console.warn = originalWarn;
  }

  assert.equal(warnings.length, 2);
  assert.match(warnings[0], /unoptimized WASM runtime/);
  assert.equal(warnings[1], "Install it with: dotnet workload install wasm-tools");
});
