/** Maximum portable input size shared with the .NET Core boundary. */
export const MAX_INPUT_BYTES = 64 * 1024 * 1024;

/** Converts supported chapter input values to UTF-8 or binary bytes. */
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

/** Validates and normalizes an optional source file name. */
export function requireFileName(fileName: unknown): string {
  if (fileName === undefined) {
    return "input.txt";
  }

  if (typeof fileName !== "string" || fileName.trim().length === 0) {
    throw new TypeError("fileName must be a non-empty string.");
  }

  return fileName.trim();
}
