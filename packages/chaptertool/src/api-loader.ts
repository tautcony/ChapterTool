/**
 * Creates a shared asynchronous loader that can retry after a failed load.
 * Concurrent callers share the same pending or successful Promise.
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
