import { FileSpreadsheet, Upload, X } from 'lucide-react';
import { useId, useRef, useState, type DragEvent, type ReactNode } from 'react';
import { EXCEL_IMPORT_ACCEPT, formatExcelBytes, validateExcelFile } from './limits';
import { importCopy } from './importCopy';

type Props = {
  arabic: boolean;
  busy?: boolean;
  disabled?: boolean;
  file: File | null;
  hint?: ReactNode;
  label?: ReactNode;
  onFile: (file: File | null) => void;
};

export function ExcelDropzone({ arabic, busy, disabled, file, hint, label, onFile }: Props) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState('');

  function take(next?: File | null) {
    if (disabled || busy) return;
    setError('');
    if (!next) {
      onFile(null);
      if (inputRef.current) inputRef.current.value = '';
      return;
    }
    const problem = validateExcelFile(next, arabic);
    if (problem) {
      setError(problem);
      onFile(null);
      if (inputRef.current) inputRef.current.value = '';
      return;
    }
    onFile(next);
  }

  function onDrag(event: DragEvent, active: boolean) {
    event.preventDefault();
    if (!disabled && !busy) setDragging(active);
  }

  return (
    <div className="space-y-3">
      {label ? <p className="text-sm font-semibold text-slate-700">{label}</p> : null}
      <input
        accept={EXCEL_IMPORT_ACCEPT}
        className="sr-only"
        disabled={disabled || busy}
        id={inputId}
        onChange={(event) => take(event.target.files?.[0] ?? null)}
        ref={inputRef}
        type="file"
      />
      <label
        className={`relative flex min-h-56 cursor-pointer flex-col items-center justify-center rounded-3xl border-2 border-dashed px-6 py-10 text-center transition ${
          dragging ? 'border-mis-primary bg-mis-pale' : 'border-mis-sky/70 bg-gradient-to-b from-mis-pale/50 to-white hover:border-mis-primary hover:bg-mis-pale/40'
        } ${disabled || busy ? 'cursor-not-allowed opacity-70' : ''}`}
        htmlFor={inputId}
        onDragEnter={(event) => onDrag(event, true)}
        onDragLeave={(event) => onDrag(event, false)}
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          event.preventDefault();
          setDragging(false);
          take(event.dataTransfer.files?.[0] ?? null);
        }}
      >
        <span className="grid h-16 w-16 place-items-center rounded-2xl bg-white text-mis-primary shadow-sm">
          <Upload className="h-7 w-7" />
        </span>
        <p className="mt-4 text-lg font-bold text-mis-navy">{dragging ? importCopy.dropActive(arabic) : importCopy.dropTitle(arabic)}</p>
        <p className="mt-2 max-w-xl text-sm leading-6 text-slate-600">{importCopy.dropHelp(arabic)}</p>
        <p className="mt-4 text-xs font-semibold uppercase tracking-wide text-slate-500">{importCopy.formats(arabic)}</p>
        {hint ? <div className="mt-3 text-sm text-slate-500">{hint}</div> : null}
      </label>
      {file ? (
        <div className="flex items-center gap-3 rounded-2xl border border-mis-sky/70 bg-white px-4 py-3 shadow-sm">
          <span className="grid h-12 w-12 place-items-center rounded-xl bg-mis-pale text-mis-primary">
            <FileSpreadsheet className="h-6 w-6" />
          </span>
          <div className="min-w-0 flex-1">
            <p className="truncate font-bold text-mis-navy" dir="ltr" title={file.name}>{file.name}</p>
            <p className="mt-1 text-xs text-slate-500" dir="ltr">{formatExcelBytes(file.size, arabic)}</p>
          </div>
          <button
            className="rounded-lg p-2 text-slate-500 hover:bg-rose-50 hover:text-rose-600"
            disabled={disabled || busy}
            onClick={() => take(null)}
            type="button"
          >
            <X className="h-4 w-4" />
            <span className="sr-only">{importCopy.removeFile(arabic)}</span>
          </button>
        </div>
      ) : null}
      {error ? <p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">{error}</p> : null}
    </div>
  );
}
