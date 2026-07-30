/** Signature of the .NET export object used by {@link invokeJson}. */
type JsonExportApi = Record<string, (...arguments_: unknown[]) => unknown>;

/**
 * Serialises a JavaScript value to a JSON string for transfer to the .NET
 * Core.  Used for complex records that `JSExport` does not marshal
 * automatically.
 *
 * @param value - The value to serialise.
 * @param name - Label for the value, included in error messages.
 * @returns The JSON string representation.
 * @throws {TypeError} When {@link JSON.stringify} throws (e.g. circular
 *   reference) or returns {@code undefined}.
 */
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

/**
 * Parses a JSON string returned by a .NET export into the expected type.
 *
 * @typeParam T - Shape of the deserialised value.
 * @param value - The raw JSON string from the .NET Core.
 * @param operation - Operation name used in error messages.
 * @returns The parsed value cast to {@link T}.
 * @throws {Error} When {@link JSON.parse} fails on the input.
 */
export function decodeJson<T>(value: string, operation: string): T {
  try {
    return JSON.parse(value) as T;
  } catch (error) {
    throw new Error(`ChapterTool ${operation} returned invalid JSON.`, { cause: error });
  }
}

/**
 * Calls a named method on a .NET JSON-export API, then parses the returned
 * JSON string into the expected type.
 *
 * This is the primary bridge between JavaScript and the .NET Core for any
 * operation that returns complex data.
 *
 * @typeParam T - Expected shape of the deserialised return value.
 * @param api - The .NET export object (a map of operation name to function).
 * @param operation - Method name on the {@code api} object.
 * @param arguments_ - Positional arguments forwarded to the .NET method.
 * @returns The deserialised return value.
 * @throws {Error} When the .NET method returns text that is not valid JSON.
 */
export function invokeJson<T>(api: JsonExportApi, operation: string, ...arguments_: unknown[]): T {
  return decodeJson<T>(api[operation](...arguments_) as string, operation);
}
