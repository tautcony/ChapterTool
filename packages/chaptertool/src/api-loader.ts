/**
 * Wraps an asynchronous factory function so that concurrent callers share a
 * single initialisation attempt.  When the factory rejects, the next call
 * automatically retries — the stale promise is discarded and a fresh attempt
 * is made.
 *
 * This pattern is used to load the .NET WASM runtime lazily: the first API
 * call triggers the load, all parallel calls wait on the same promise, and a
 * transient failure (e.g. out-of-memory during startup) does not poison the
 * loader.
 *
 * @typeParam T - The type produced by the factory (typically the .NET
 *   `NodeApi` object).
 * @param load - The asynchronous factory function.  May be called multiple
 *   times across retries.
 * @returns A function that, when called, always returns a `Promise<T>` that
 *   resolves to the most recent successful output of {@code load}.  The
 *   returned function never rejects due to a previous failure; it only
 *   reflects the current attempt.
 *
 * @example
 * ```ts
 * const loadOnce = createRetryableLoader(() => connectToDatabase());
 * // Both callers share the same connection promise:
 * const [a, b] = await Promise.all([loadOnce(), loadOnce()]);
 * ```
 */
export function createRetryableLoader<T>(load: () => T | PromiseLike<T>): () => Promise<Awaited<T>> {
  let promise: Promise<Awaited<T>> | undefined;

  return () => {
    if (promise) {
      return promise;
    }

    const pending: Promise<Awaited<T>> = Promise.resolve()
      .then(load)
      .then((value) => value as Awaited<T>)
      .catch((error: unknown) => {
        promise = undefined;
        throw error;
      });
    promise = pending;

    return pending;
  };
}
