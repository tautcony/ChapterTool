/** Converts supported chapter input values to UTF-8 or binary bytes. */
export function toBytes(content: string | Buffer | Uint8Array): Buffer {
  if (typeof content === "string") {
    return Buffer.from(content, "utf8");
  }

  if (Buffer.isBuffer(content)) {
    return content;
  }

  if (content instanceof Uint8Array) {
    return Buffer.from(content);
  }

  throw new TypeError("Chapter content must be a string, Buffer, or Uint8Array.");
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
