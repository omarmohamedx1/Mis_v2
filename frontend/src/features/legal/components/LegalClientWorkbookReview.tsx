import { ProfessionalSelect } from '../../../components/forms/ProfessionalSelect';
import { useLegalText } from '../legalUi';
import { legalClientImportFields, suggestWorkbookMapping, workbookHeaders } from '../legalClientWorkbook';
import type { LegalClientColumnMapping, LegalClientWorkbook } from '../types/legal';

interface Props {
  workbook: LegalClientWorkbook;
  mapping: LegalClientColumnMapping;
  disabled: boolean;
  onChange: (mapping: LegalClientColumnMapping) => void;
}

export function LegalClientWorkbookReview({ workbook, mapping, disabled, onChange }: Props) {
  const l = useLegalText();
  const sheetIndex = Math.max(0, workbook.sheets.findIndex(sheet => sheet.name === mapping.sheetName));
  const sheet = workbook.sheets[sheetIndex];
  const headers = workbookHeaders(sheet.rows, mapping.headerRow);
  const samples = sheet.rows.slice(mapping.headerRow, mapping.headerRow + 10);
  const mappedColumns = new Set(Object.values(mapping.columns).filter(Boolean));
  const extraColumns = headers.filter(header => !mappedColumns.has(header));
  const retained = mapping.extraColumns ?? headers;
  const selectedExtras = extraColumns.filter(header => retained.includes(header));
  return (
    <section className="space-y-5 rounded-2xl border border-mis-border bg-white p-5 shadow-sm">
      <div>
        <h2 className="font-bold text-mis-navy">{l.text('١. افتح الملف وحدد أعمدة العملاء', '1. Review the workbook and select client columns')}</h2>
        <p className="mt-1 text-sm text-slate-600">{l.text('مش لازم تغيّر أسماء الأعمدة في Excel. اختَر الشيت وصف العناوين، وراجع الربط المقترح تحت.', 'Keep your Excel column names. Choose a sheet and header row, then review the suggested mapping below.')}</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span className="block">{l.text('الشيت', 'Worksheet')}</span>
          <ProfessionalSelect aria-label={l.text('الشيت', 'Worksheet')} disabled={disabled} value={String(sheetIndex)} onChange={event => onChange(suggestWorkbookMapping(workbook, Number(event.target.value)))}>
            {workbook.sheets.map((item, index) => <option disabled={!item.rows.some(row => row.some(cell => cell.trim()))} key={index} value={index}>{item.name || 'CSV'}</option>)}
          </ProfessionalSelect>
        </label>
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span className="block">{l.text('صف عناوين الأعمدة', 'Column header row')}</span>
          <ProfessionalSelect aria-label={l.text('صف عناوين الأعمدة', 'Column header row')} disabled={disabled} value={String(mapping.headerRow)} onChange={event => onChange(suggestWorkbookMapping(workbook, sheetIndex, Number(event.target.value)))}>
            {sheet.rows.map((row, index) => <option key={index} value={index + 1}>{l.text('الصف', 'Row')} {index + 1}: {row.filter(Boolean).slice(0, 3).join(' · ') || l.text('فارغ', 'Empty')}</option>)}
          </ProfessionalSelect>
        </label>
      </div>
      <div className="overflow-auto rounded-xl border border-mis-border">
        <table className="w-full text-sm">
          <thead className="bg-slate-50"><tr><th className="whitespace-nowrap px-3 py-3 text-start">{l.text('صف Excel', 'Excel row')}</th>{headers.map(header => <th className="min-w-40 max-w-64 break-words px-3 py-3 text-start" key={header}>{header}</th>)}</tr></thead>
          <tbody className="divide-y divide-mis-border">
            {samples.map((row, index) => <tr key={index}><td className="px-3 py-3 text-slate-500">{mapping.headerRow + index + 1}</td>{headers.map((header, column) => <td className="max-w-64 break-words px-3 py-3 text-slate-700" key={header}>{row[column] || '—'}</td>)}</tr>)}
          </tbody>
        </table>
      </div>
      <p className="text-xs text-slate-500">{l.text('المعروض عينة من أول ٢٥ صفًا لاختيار العناوين. التحقق التالي يقرأ كل صفوف الشيت المحدد، حتى ٥٠٠٠ صف.', 'This sample comes from the first 25 rows to help select headers. Validation reads every data row in the selected sheet, up to 5,000 rows.')}</p>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {legalClientImportFields.map(field => <label className="space-y-1.5 text-sm font-semibold text-slate-700" key={field.key}>
          <span className="block">{l.ar ? field.ar : field.en}{field.required ? ' *' : ''}</span>
          <ProfessionalSelect aria-label={l.ar ? field.ar : field.en} disabled={disabled} value={mapping.columns[field.key] || ''} onChange={event => onChange({ ...mapping, columns: { ...mapping.columns, [field.key]: event.target.value } })}>
            <option value="">{l.text('لم يتم الربط', 'Not mapped')}</option>
            {headers.map(header => <option disabled={Object.entries(mapping.columns).some(([key, value]) => key !== field.key && value === header)} key={header} value={header}>{header}</option>)}
          </ProfessionalSelect>
        </label>)}
      </div>
      <div className="space-y-3 rounded-2xl border border-mis-border bg-slate-50/60 p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="font-bold text-mis-navy">{l.text('بيانات إضافية تحفظ مع العميل', 'Additional data to keep with the client')}</h3>
            <p className="mt-1 text-sm font-normal text-slate-600">{l.text('اختَر أي أعمدة زيادة تحتاجها، زي المديونية أو الفرع أو تاريخ المحضر. هتتحفظ باسم العمود وقيمته وتظهر في ملف العميل، بدون إنشاء خانات جديدة.', 'Select extra columns such as balances, branches, or report dates. Their names and values are saved and displayed in the client file.')}</p>
          </div>
          {extraColumns.length ? <button className="rounded-xl border border-mis-control bg-white px-3 py-2 text-sm font-semibold text-mis-primary disabled:opacity-50" disabled={disabled} onClick={() => onChange({ ...mapping, extraColumns: selectedExtras.length === extraColumns.length ? [] : [...extraColumns] })} type="button">{selectedExtras.length === extraColumns.length ? l.text('إلغاء تحديد الإضافية', 'Deselect extra columns') : l.text('تحديد كل الإضافية', 'Select all extra columns')}</button> : null}
        </div>
        {extraColumns.length ? <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          {extraColumns.map(header => <label className={`flex min-w-0 items-center gap-3 rounded-xl border px-3 py-3 text-sm ${retained.includes(header) ? 'border-mis-primary/30 bg-mis-pale/60' : 'border-mis-border bg-white'}`} key={header}>
            <input checked={retained.includes(header)} disabled={disabled} onChange={event => onChange({ ...mapping, extraColumns: event.target.checked ? [...retained, header] : retained.filter(item => item !== header) })} type="checkbox" className="h-4 w-4 shrink-0 accent-mis-primary" />
            <span className="min-w-0 break-words font-semibold text-slate-700">{header}</span>
          </label>)}
        </div> : <p className="text-sm text-slate-500">{l.text('كل أعمدة الملف مرتبطة بالفعل بالبيانات الأساسية.', 'Every column is already mapped to a client field.')}</p>}
        <p className="text-xs text-slate-500">{l.text('محدد {count} عمود إضافي. الأعمدة المرتبطة بالبيانات الأساسية تتحفظ تلقائيًا.', '{count} extra columns selected. Columns mapped to client fields are always retained.').replace('{count}', l.number(selectedExtras.length))}</p>
      </div>
      <p className="rounded-xl bg-mis-pale/50 px-4 py-3 text-sm text-mis-navy">{l.text('للحفظ: اربط اسم العميل ومعرّف واحد على الأقل (الرقم القومي أو الموبايل أو رقم الحساب)، وحدد البيانات الإضافية التي تحتاجها.', 'To save, map the client name and at least one identifier (national ID, mobile, or account number), and select the extra data you need.')}</p>
    </section>
  );
}
