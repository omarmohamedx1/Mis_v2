export function preferredHrImportSheetIndex(sheets: Array<{ sheetName?: string | null }>): number {
  const index = sheets.findIndex((sheet) => {
    const name = sheet.sheetName ?? '';
    return name.length > 0 && !/instruction|تعليمات|reference data/i.test(name);
  });
  return index >= 0 ? index : 0;
}
