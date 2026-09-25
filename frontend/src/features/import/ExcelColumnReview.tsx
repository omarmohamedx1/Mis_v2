import { CheckCircle2, SlidersHorizontal } from 'lucide-react';
import type { ReactNode } from 'react';
import type { ImportFieldDef } from './excelAutoMap';
import { extraImportColumns, missingRequiredImportFields } from './excelAutoMap';
import { importCopy } from './importCopy';

type Props = {
  advanced?: ReactNode;
  arabic: boolean;
  detectedColumns: readonly string[];
  extraSelected: string[];
  fields: readonly ImportFieldDef[];
  mapping: Record<string, string | null | undefined>;
  onExtraSelected: (columns: string[]) => void;
  onToggleAdvanced?: () => void;
  showAdvanced?: boolean;
};

export function ExcelColumnReview({
  advanced,
  arabic,
  detectedColumns,
  extraSelected,
  fields,
  mapping,
  onExtraSelected,
  onToggleAdvanced,
  showAdvanced,
}: Props) {
  const extras = extraImportColumns(detectedColumns, mapping);
  const missing = missingRequiredImportFields(fields, mapping);
  const mapped = fields.filter((field) => mapping[field.key]?.trim());

  function toggleExtra(column: string) {
    onExtraSelected(extraSelected.includes(column) ? extraSelected.filter((item) => item !== column) : [...extraSelected, column]);
  }

  return (
    <div className="space-y-4">
      <section className="rounded-2xl border border-emerald-200 bg-emerald-50/70 p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <CheckCircle2 className="mt-0.5 h-5 w-5 text-emerald-700" />
            <div>
              <h3 className="font-bold text-emerald-950">{importCopy.columnsTitle(arabic)}</h3>
              <p className="mt-1 text-sm text-emerald-900/80">{importCopy.columnsHelp(arabic)}</p>
            </div>
          </div>
          {onToggleAdvanced ? (
            <button className="inline-flex items-center gap-2 rounded-lg border border-emerald-300 bg-white px-3 py-1.5 text-xs font-bold text-emerald-800" onClick={onToggleAdvanced} type="button">
              <SlidersHorizontal className="h-3.5 w-3.5" />
              {showAdvanced ? importCopy.hideAdvanced(arabic) : importCopy.advancedMapping(arabic)}
            </button>
          ) : null}
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          {mapped.map((field) => (
            <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-emerald-800 shadow-sm" key={field.key}>
              {arabic ? field.ar : field.en}: {mapping[field.key]}
            </span>
          ))}
        </div>
        {missing.length ? (
          <p className="mt-3 text-sm font-semibold text-amber-800">
            {arabic ? 'أعمدة مطلوبة مش متعرفة:' : 'Required columns were not recognized:'} {missing.map((field) => (arabic ? field.ar : field.en)).join(' · ')}
          </p>
        ) : null}
      </section>

      <section className="rounded-2xl border border-mis-border bg-white p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="font-bold text-mis-navy">{importCopy.extrasTitle(arabic)}</h3>
            <p className="mt-1 text-sm text-slate-600">{importCopy.extrasHelp(arabic)}</p>
          </div>
          {extras.length ? (
            <button
              className="rounded-lg border border-mis-border px-3 py-1.5 text-xs font-bold text-mis-primary hover:bg-mis-pale"
              onClick={() => onExtraSelected(extraSelected.length === extras.length ? [] : [...extras])}
              type="button"
            >
              {extraSelected.length === extras.length ? importCopy.hideAllExtras(arabic) : importCopy.showAllExtras(arabic)}
            </button>
          ) : null}
        </div>
        {extras.length === 0 ? (
          <p className="mt-4 text-sm text-slate-500">{importCopy.noExtras(arabic)}</p>
        ) : (
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            {extras.map((column) => {
              const checked = extraSelected.includes(column);
              return (
                <label className={`flex items-center justify-between gap-3 rounded-xl border px-3 py-2 text-sm ${checked ? 'border-mis-primary bg-mis-pale/70' : 'border-mis-border bg-slate-50'}`} key={column}>
                  <span className="min-w-0 truncate font-semibold text-mis-navy">{column}</span>
                  <span className="flex items-center gap-2">
                    <span className="text-xs text-slate-500">{checked ? importCopy.showExtra(arabic) : importCopy.hideExtra(arabic)}</span>
                    <input checked={checked} onChange={() => toggleExtra(column)} type="checkbox" />
                  </span>
                </label>
              );
            })}
          </div>
        )}
      </section>

      {showAdvanced ? advanced : null}
    </div>
  );
}
