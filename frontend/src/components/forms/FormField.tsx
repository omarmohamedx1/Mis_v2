import type { ReactNode } from 'react';

export interface FormFieldProps {
  children: ReactNode;
  className?: string;
  error?: ReactNode;
  errorId?: string;
  hint?: ReactNode;
  hintId?: string;
  inputId: string;
  label: ReactNode;
  required?: boolean;
}

export const formControlClass = 'form-control w-full rounded-form border border-mis-control bg-white text-sm text-mis-ink transition placeholder:text-mis-muted focus:border-mis-blue focus:shadow-input focus:outline-none disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500';

export function joinDescribedBy(...values: Array<string | undefined>): string | undefined {
  const ids = values.filter(Boolean);
  return ids.length ? ids.join(' ') : undefined;
}

export function FormField({ children, className = '', error, errorId, hint, hintId, inputId, label, required = false }: FormFieldProps) {
  return (
    <div className={`min-w-0 space-y-2 [overflow-wrap:anywhere] ${className}`}>
      <label className="block text-sm font-semibold text-slate-700" htmlFor={inputId}>
        {label}
        {required ? <span className="ms-1 text-red-600" aria-hidden="true">*</span> : null}
      </label>
      {children}
      {hint ? <p className="text-xs leading-5 text-slate-500" id={hintId}>{hint}</p> : null}
      {error ? <p className="text-sm text-red-600" id={errorId} role="alert">{error}</p> : null}
    </div>
  );
}
