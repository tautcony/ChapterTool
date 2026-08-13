/**
 * Fallback portable input size (64 MiB) used before the .NET runtime reports
 * the live Core byte limit.
 */
const DEFAULT_MAX_INPUT_BYTES = 64 * 1024 * 1024;

/**
 * Maximum portable input size shared with the .NET Core boundary.
 * The Node host overwrites this value from {@code NodeApi.GetMaxInputBytes}
 * after the WebAssembly runtime starts.
 */
export let MAX_INPUT_BYTES = DEFAULT_MAX_INPUT_BYTES;

/**
 * Replaces the JavaScript-side byte limit with the live Core value.
 *
 * @param value - Byte limit reported by {@code NodeApi.GetMaxInputBytes}.
 */
export function applyMaxInputBytes(value: number): void {
  if (Number.isFinite(value) && value > 0) {
    MAX_INPUT_BYTES = value;
  }
}

/**
 * Converts supported chapter input values to a UTF-8 {@link Buffer} suitable
 * for base64 encoding and transfer to the .NET Core.
 *
 * @param content - Chapter source: a UTF-8 string, a Node.js `Buffer`, or a
 *   `Uint8Array` of raw bytes.
 * @returns A `Buffer` containing UTF-8 bytes (for strings) or the raw bytes
 *   unchanged.
 * @throws {TypeError} When {@code content} is not a string, `Buffer`, or
 *   `Uint8Array`.
 * @throws {RangeError} When the byte length exceeds {@link MAX_INPUT_BYTES}.
 */
export function toBytes(content: string | Buffer | Uint8Array): Buffer {
  if (typeof content === "string") {
    ensureInputSize(Buffer.byteLength(content, "utf8"));
    return Buffer.from(content, "utf8");
  }

  if (Buffer.isBuffer(content)) {
    ensureInputSize(content.byteLength);
    return content;
  }

  if (content instanceof Uint8Array) {
    ensureInputSize(content.byteLength);
    return Buffer.from(content);
  }

  throw new TypeError("Chapter content must be a string, Buffer, or Uint8Array.");
}

/**
 * Enforces the shared byte-limit before passing data to the WASM boundary.
 *
 * @param byteCount - Number of bytes the input will occupy.
 * @throws {RangeError} With {@code code: "INPUT_TOO_LARGE"} and
 *   {@code maxBytes}/{@code actualBytes} properties when the limit is exceeded.
 */
function ensureInputSize(byteCount: number): void {
  if (byteCount > MAX_INPUT_BYTES) {
    const error = new RangeError(`Chapter input exceeds the ${MAX_INPUT_BYTES} byte limit.`) as RangeError & {
      code?: string;
      maxBytes?: number;
      actualBytes?: number;
    };
    error.code = "INPUT_TOO_LARGE";
    error.maxBytes = MAX_INPUT_BYTES;
    error.actualBytes = byteCount;
    throw error;
  }
}

/**
 * Validates and normalises an optional source file name for the import API.
 *
 * @param fileName - Value from the import {@code options.fileName}.  When
 *   {@code undefined} the default {@code "input.txt"} is returned.
 * @returns A trimmed, non-empty file name string.
 * @throws {TypeError} When {@code fileName} is not a string or trims to empty.
 */
export function requireFileName(fileName: unknown): string {
  if (fileName === undefined) {
    return "input.txt";
  }

  if (typeof fileName !== "string" || fileName.trim().length === 0) {
    throw new TypeError("fileName must be a non-empty string.");
  }

  return fileName.trim();
}
