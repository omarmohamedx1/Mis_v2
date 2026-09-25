const storageKey = (scope: string) => `mis.import.visibleExtras.${scope}`;

export function loadVisibleExtraColumns(scope: string): string[] {
  try {
    const raw = localStorage.getItem(storageKey(scope));
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

export function saveVisibleExtraColumns(scope: string, columns: readonly string[]): void {
  localStorage.setItem(storageKey(scope), JSON.stringify([...new Set(columns)]));
}

export function rememberExtraColumns(scope: string, columns: readonly string[]): string[] {
  const next = [...new Set([...loadVisibleExtraColumns(scope), ...columns])];
  saveVisibleExtraColumns(scope, next);
  return next;
}

export function extrasFromRecord(values: Record<string, string | null | undefined> | null | undefined, known: readonly string[]): Array<[string, string]> {
  if (!values) return [];
  const claimed = new Set(known.map((item) => item.trim().toLowerCase()));
  return Object.entries(values)
    .filter(([key, value]) => key && key !== '_sheet' && !claimed.has(key.trim().toLowerCase()) && Boolean(value?.trim()))
    .map(([key, value]) => [key, value!.trim()]);
}
