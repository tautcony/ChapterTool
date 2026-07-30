/**
 * Asserts that a value is a non-array, non-null object and returns it as a
 * typed record.  Used to validate API inputs before they cross the WASM
 * boundary.
 *
 * @param value - The value to check.
 * @param name - Argument name for the error message.
 * @returns The value narrowed to `Record<string, unknown>`.
 * @throws {TypeError} When the value is {@code null}, an array, or not an
 *   object.
 */
export function requireObject(value: unknown, name: string): Record<string, unknown> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new TypeError(`${name} must be an object.`);
  }

  return value as Record<string, unknown>;
}

/**
 * Asserts that a value is a string.
 *
 * @param value - The value to check.
 * @param name - Argument name for the error message.
 * @returns The value narrowed to `string`.
 * @throws {TypeError} When the value is not a string.
 */
export function requireString(value: unknown, name: string): string {
  if (typeof value !== "string") {
    throw new TypeError(`${name} must be a string.`);
  }

  return value;
}

/**
 * Asserts that a value is a boolean.
 *
 * @param value - The value to check.
 * @param name - Argument name for the error message.
 * @returns The value narrowed to `boolean`.
 * @throws {TypeError} When the value is not a boolean.
 */
export function requireBoolean(value: unknown, name: string): boolean {
  if (typeof value !== "boolean") {
    throw new TypeError(`${name} must be a boolean.`);
  }

  return value;
}

/**
 * Asserts that a value is a safe integer (within
 * {@code Number.MIN_SAFE_INTEGER} … {@code Number.MAX_SAFE_INTEGER}).
 *
 * @param value - The value to check.
 * @param name - Argument name for the error message.
 * @returns The value narrowed to `number`.
 * @throws {TypeError} When the value is not a safe integer.
 */
export function requireInteger(value: unknown, name: string): number {
  if (!Number.isSafeInteger(value)) {
    throw new TypeError(`${name} must be a safe integer.`);
  }

  return value as number;
}

/**
 * Asserts that a value is a non-negative safe integer (a valid chapter index).
 *
 * @param value - The value to check.
 * @param name - Argument name for the error message.
 * @returns The value narrowed to `number` (≥ 0).
 * @throws {TypeError} When the value is not a safe integer.
 * @throws {RangeError} When the value is negative.
 */
export function requireIndex(value: unknown, name: string): number {
  const index = requireInteger(value, name);
  if (index < 0) {
    throw new RangeError(`${name} must be a non-negative integer.`);
  }

  return index;
}

/**
 * Asserts that a value is a finite number (not {@code NaN} or ±Infinity).
 *
 * @param value - The value to check.
 * @param name - Argument name for the error message.
 * @returns The value narrowed to `number`.
 * @throws {TypeError} When the value is not a finite number.
 */
export function requireNumber(value: unknown, name: string): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new TypeError(`${name} must be a finite number.`);
  }

  return value;
}

/**
 * Asserts that a value is an array of non-negative safe integers (chapter
 * indexes).  Used to validate the {@code indexes} argument of
 * {@code ChapterTool.delete} and {@code ChapterTool.createZones}.
 *
 * @param indexes - The value to validate.
 * @returns The value narrowed to `number[]`.
 * @throws {TypeError} When the value is not an array, or any element is not a
 *   non-negative safe integer.
 */
export function requireIndexes(indexes: unknown): number[] {
  if (!Array.isArray(indexes) || indexes.some((index) => !Number.isSafeInteger(index) || index < 0)) {
    throw new TypeError("indexes must be an array of non-negative chapter indexes.");
  }

  return indexes as number[];
}
