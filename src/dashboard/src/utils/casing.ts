/**
 * Generic camelCase/PascalCase key lookup for API payloads whose casing
 * differs from route to route. Readers keep their own result type and use
 * `pickCase` at the data-access boundary, so both casings keep working.
 */

/** Returns the key with the case of its first letter flipped. */
const flipFirstLetter = (key: string): string => {
  const first = key.charAt(0);
  return first === first.toUpperCase()
    ? first.toLowerCase() + key.slice(1)
    : first.toUpperCase() + key.slice(1);
};

/** Reads a string field, defaulting to '' for anything else. */
export const asString = (raw: unknown): string => (typeof raw === 'string' ? raw : '');

/** Reads a count field, defaulting to 0 for anything else. */
export const asCount = (raw: unknown): number => (typeof raw === 'number' ? raw : 0);

/**
 * Picks a value under the camelCase or PascalCase spelling of a key.
 * An explicit `null` counts as a value and is never replaced by the other casing.
 */
export const pickCase = <T>(source: unknown, key: string): T | undefined => {
  if (typeof source !== 'object' || source === null) return undefined;
  const record = source as Record<string, unknown>;
  if (key in record) return record[key] as T;
  const flipped = flipFirstLetter(key);
  if (flipped in record) return record[flipped] as T;
  return undefined;
};
