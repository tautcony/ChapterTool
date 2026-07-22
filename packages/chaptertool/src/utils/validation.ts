export function requireObject(value: unknown, name: string): Record<string, unknown> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new TypeError(`${name} must be an object.`);
  }

  return value as Record<string, unknown>;
}

export function requireString(value: unknown, name: string): string {
  if (typeof value !== "string") {
    throw new TypeError(`${name} must be a string.`);
  }

  return value;
}

export function requireBoolean(value: unknown, name: string): boolean {
  if (typeof value !== "boolean") {
    throw new TypeError(`${name} must be a boolean.`);
  }

  return value;
}

export function requireInteger(value: unknown, name: string): number {
  if (!Number.isSafeInteger(value)) {
    throw new TypeError(`${name} must be a safe integer.`);
  }

  return value as number;
}

export function requireIndex(value: unknown, name: string): number {
  const index = requireInteger(value, name);
  if (index < 0) {
    throw new RangeError(`${name} must be a non-negative integer.`);
  }

  return index;
}

export function requireNumber(value: unknown, name: string): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new TypeError(`${name} must be a finite number.`);
  }

  return value;
}

export function requireIndexes(indexes: unknown): number[] {
  if (!Array.isArray(indexes) || indexes.some((index) => !Number.isSafeInteger(index) || index < 0)) {
    throw new TypeError("indexes must be an array of non-negative chapter indexes.");
  }

  return indexes as number[];
}
