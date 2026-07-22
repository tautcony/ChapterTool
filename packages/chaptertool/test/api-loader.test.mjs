import { describe, expect, it, vi } from "vitest";

import { createRetryableLoader } from "../src/api-loader.ts";

describe("createRetryableLoader", () => {
it("retries a failed load and shares a later successful load", async () => {
  let attempts = 0;
  const loadImplementation = vi.fn(async () => {
    attempts += 1;
    if (attempts === 1) {
      throw new Error("startup failed");
    }
    return { attempts };
  });
  const load = createRetryableLoader(loadImplementation);

  await expect(load()).rejects.toThrow("startup failed");

  const [first, second] = await Promise.all([load(), load()]);
  expect(first).toBe(second);
  expect(first).toEqual({ attempts: 2 });
  expect(attempts).toBe(2);
  expect(loadImplementation).toHaveBeenCalledTimes(2);
});
});
