export interface ImportFieldDef {
  key: string;
  en: string;
  ar: string;
  required?: boolean;
  aliases: readonly string[];
}

export function normalizeImportHeader(value: string): string {
  return value
    .normalize('NFKC')
    .replace(/^\uFEFF/, '')
    .replace(/[\u200B-\u200D\uFEFF\u00A0\u202F\u2007\u2060]/g, ' ')
    .replace(/[_./\\:|()]+/g, ' ')
    .replace(/[-–—−]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

export function compactImportHeader(value: string): string {
  return normalizeImportHeader(value).replace(/[^a-z0-9\u0600-\u06ff]+/gi, '');
}

export function isInstructionSheetName(name?: string | null): boolean {
  return Boolean(name && /instruction|تعليمات|reference data|مرجع/i.test(name));
}

export function scoreAliasMatch(column: string, alias: string): number {
  const normalizedColumn = normalizeImportHeader(column);
  const normalizedAlias = normalizeImportHeader(alias);
  if (!normalizedColumn || !normalizedAlias) return 0;
  if (normalizedColumn === normalizedAlias) return 100 + normalizedAlias.length;
  const compactColumn = compactImportHeader(column);
  const compactAlias = compactImportHeader(alias);
  if (compactColumn && compactColumn === compactAlias) return 90 + normalizedAlias.length;
  const aliasWords = normalizedAlias.split(' ').filter(Boolean);
  if (aliasWords.length >= 2) {
    if (` ${normalizedColumn} `.includes(` ${normalizedAlias} `)) return 70 + normalizedAlias.length;
    if (compactColumn.startsWith(compactAlias) || compactColumn.endsWith(compactAlias)) return 60 + normalizedAlias.length;
  }
  if (aliasWords.length === 1 && (compactColumn.includes(compactAlias) || compactAlias.includes(compactColumn)) && compactAlias.length >= 4) {
    return 35 + Math.min(compactAlias.length, compactColumn.length);
  }
  return 0;
}

export function autoMapImportColumns(fields: readonly ImportFieldDef[], detectedColumns: readonly string[]): Record<string, string> {
  const columns: Record<string, string> = Object.fromEntries(fields.map((field) => [field.key, '']));
  const claimed = new Set<string>();

  for (const field of fields) {
    let bestColumn = '';
    let bestScore = 0;
    for (const column of detectedColumns) {
      if (claimed.has(column) || !column?.trim()) continue;
      const labels = [field.en, field.ar, ...field.aliases];
      for (const alias of labels) {
        const score = scoreAliasMatch(column, alias);
        if (score > bestScore) {
          bestScore = score;
          bestColumn = column;
        }
      }
    }
    if (bestColumn) {
      columns[field.key] = bestColumn;
      claimed.add(bestColumn);
    }
  }

  return columns;
}

export function extraImportColumns(detectedColumns: readonly string[], mapping: Record<string, string | null | undefined>): string[] {
  const claimed = new Set(Object.values(mapping).filter((value): value is string => Boolean(value?.trim())));
  return detectedColumns.filter((column) => column?.trim() && !claimed.has(column));
}

export function missingRequiredImportFields(
  fields: readonly ImportFieldDef[],
  mapping: Record<string, string | null | undefined>,
  satisfy?: (field: ImportFieldDef, mapping: Record<string, string | null | undefined>) => boolean | undefined,
): ImportFieldDef[] {
  return fields.filter((field) => {
    const custom = satisfy?.(field, mapping);
    if (custom !== undefined) return !custom;
    return Boolean(field.required) && !mapping[field.key]?.trim();
  });
}

export function scoreSheetMapping(fields: readonly ImportFieldDef[], detectedColumns: readonly string[]): number {
  const mapped = autoMapImportColumns(fields, detectedColumns);
  const mappedCount = Object.values(mapped).filter((value) => value.trim()).length;
  const missing = missingRequiredImportFields(fields, mapped).length;
  return mappedCount * 10 - missing * 50 + detectedColumns.filter((column) => /[a-z\u0600-\u06ff]/i.test(column)).length;
}

export function pickBestImportSheetIndex(fields: readonly ImportFieldDef[], sheets: readonly { sheetName?: string | null; detectedColumns: readonly string[] }[]): number {
  let bestIndex = 0;
  let bestScore = Number.NEGATIVE_INFINITY;
  sheets.forEach((sheet, index) => {
    if (isInstructionSheetName(sheet.sheetName)) return;
    const score = scoreSheetMapping(fields, sheet.detectedColumns);
    if (score > bestScore) {
      bestScore = score;
      bestIndex = index;
    }
  });
  return bestIndex;
}

export function sheetNamesFromIndexes(sheets: readonly { sheetName?: string | null }[], indexes: number[]): string[] {
  return indexes.map((index) => sheets[index]?.sheetName).filter((name): name is string => Boolean(name));
}

export function defaultSelectedSheetIndexes(sheets: readonly { sheetName?: string | null; detectedColumns?: readonly string[] }[]): number[] {
  const usable = sheets
    .map((sheet, index) => ({ index, skip: isInstructionSheetName(sheet.sheetName) }))
    .filter((item) => !item.skip)
    .map((item) => item.index);
  return usable.length > 0 ? usable : sheets.map((_, index) => index);
}
