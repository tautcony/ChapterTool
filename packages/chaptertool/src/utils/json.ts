type JsonExportApi = Record<string, (...arguments_: unknown[]) => unknown>;

/** Encodes a value for a .NET export. */
export function encodeJson(value: unknown, name: string): string {
  let json: string | undefined;
  try {
    json = JSON.stringify(value);
  } catch (error) {
    throw new TypeError(`${name} must be serializable as JSON.`, { cause: error });
  }

  if (typeof json !== "string") {
    throw new TypeError(`${name} must be serializable as JSON.`);
  }

  return json;
}

/** Decodes one JSON response from a .NET export. */
export function decodeJson<T>(value: string, operation: string): T {
  try {
    return JSON.parse(value) as T;
  } catch (error) {
    throw new Error(`ChapterTool ${operation} returned invalid JSON.`, { cause: error });
  }
}

/** Calls a .NET export that returns a JSON string. */
export function invokeJson<T>(api: JsonExportApi, operation: string, ...arguments_: unknown[]): T {
  return decodeJson<T>(api[operation](...arguments_) as string, operation);
}
