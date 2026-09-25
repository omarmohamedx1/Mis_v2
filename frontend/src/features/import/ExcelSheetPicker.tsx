import { FileSpreadsheet } from 'lucide-react';
import { defaultSelectedSheetIndexes, isInstructionSheetName } from './excelAutoMap';
import { importCopy } from './importCopy';

export type ExcelSheetOption = {
  sheetName?: string | null;
  detectedColumns?: readonly string[];
};

type Props = {
  arabic: boolean;
  onChange: (indexes: number[]) => void;
  selected: number[];
  sheets: readonly ExcelSheetOption[];
};

export function ExcelSheetPicker({ arabic, onChange, selected, sheets }: Props) {
  if (sheets.length <= 1) return null;

  const usable = defaultSelectedSheetIndexes(sheets);
  const allSelected = usable.length > 0 && usable.every((index) => selected.includes(index));

  function toggle(index: number) {
    onChange(selected.includes(index) ? selected.filter((item) => item !== index) : [...selected, index].sort((a, b) => a - b));
  }

  return (
    <section className="rounded-2xl border border-mis-border bg-white p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="font-bold text-mis-navy">{importCopy.sheetsTitle(arabic)}</h3>
          <p className="mt-1 text-sm text-slate-600">{importCopy.sheetsHelp(arabic)}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button className="rounded-lg border border-mis-border px-3 py-1.5 text-xs font-bold text-mis-primary hover:bg-mis-pale" onClick={() => onChange(usable)} type="button">
            {importCopy.selectAllSheets(arabic)}
          </button>
          <button
            className="rounded-lg border border-mis-border px-3 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-50"
            onClick={() => onChange(usable.slice(0, 1))}
            type="button"
          >
            {importCopy.selectBestSheet(arabic)}
          </button>
        </div>
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        {sheets.map((sheet, index) => {
          const instruction = isInstructionSheetName(sheet.sheetName);
          const checked = selected.includes(index);
          const name = sheet.sheetName || (arabic ? 'CSV' : 'CSV');
          return (
            <label
              className={`flex cursor-pointer items-start gap-3 rounded-2xl border px-4 py-3 ${checked ? 'border-mis-primary bg-mis-pale/60' : 'border-mis-border bg-slate-50'}`}
              key={`${name}-${index}`}
            >
              <input checked={checked} className="mt-1" onChange={() => toggle(index)} type="checkbox" />
              <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-white text-mis-primary shadow-sm">
                <FileSpreadsheet className="h-4 w-4" />
              </span>
              <span className="min-w-0">
                <span className="block font-semibold text-mis-navy">{name}</span>
                <span className="mt-1 block text-xs text-slate-500">
                  {arabic ? `${sheet.detectedColumns?.length ?? 0} عمود` : `${sheet.detectedColumns?.length ?? 0} columns`}
                  {instruction ? ` · ${importCopy.sheetSkipped(arabic)}` : ''}
                </span>
              </span>
            </label>
          );
        })}
      </div>
      <p className="mt-3 text-xs text-slate-500">
        {arabic
          ? allSelected
            ? 'هيتقرأ كل الشيتات المحددة مع بعض.'
            : `${selected.length} شيت محدد.`
          : allSelected
            ? 'All selected sheets will be read together.'
            : `${selected.length} sheet(s) selected.`}
      </p>
    </section>
  );
}
