import { FileSpreadsheet } from 'lucide-react';
import { importCopy } from './importCopy';

type Props = {
  arabic: boolean;
  fields: Array<[string, string]>;
};

export function ExtraFieldsPanel({ arabic, fields }: Props) {
  if (fields.length === 0) return null;
  return (
    <article className="min-w-0 overflow-hidden rounded-2xl border border-mis-border bg-white p-5 shadow-sm sm:p-6">
      <h2 className="mb-2 flex items-center gap-2 text-lg font-bold text-mis-navy">
        <FileSpreadsheet className="h-5 w-5 text-mis-primary" />
        {importCopy.extraFileFields(arabic)}
      </h2>
      <p className="mb-5 text-sm text-slate-600">{importCopy.extraFileFieldsHelp(arabic)}</p>
      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {fields.map(([label, value]) => (
          <div className="rounded-xl bg-slate-50 p-4" key={label}>
            <dt className="text-xs font-semibold text-slate-500">{label}</dt>
            <dd className="mt-2 break-words text-sm font-semibold text-mis-navy">{value}</dd>
          </div>
        ))}
      </dl>
    </article>
  );
}
