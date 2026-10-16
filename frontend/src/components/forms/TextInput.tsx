import { twMerge } from 'tailwind-merge';
import { useId, type InputHTMLAttributes } from 'react';

interface TextInputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
  containerClassName?: string;
}

export function TextInput({ id, label, error, hint, className = '', containerClassName = '', ...props }: TextInputProps) {
  const generatedId = useId();
  const inputId = id ?? props.name ?? generatedId;
  const errorId = error && inputId ? `${inputId}-error` : undefined;
  const isTechnicalValue = ['date', 'datetime-local', 'email', 'number', 'tel', 'time', 'url'].includes(props.type ?? '')
    || /(?:account|code|employeeNumber|hash|iban|id|number)$/i.test(props.name ?? '');

  return (
    <div className={`min-w-0 space-y-2 [overflow-wrap:anywhere] ${containerClassName}`}>
      <label className="block text-sm font-semibold text-slate-700" htmlFor={inputId}>
        {label}
      </label>
      <input
        id={inputId}
        aria-invalid={Boolean(error)}
        aria-describedby={errorId}
        dir={props.dir ?? (isTechnicalValue ? 'ltr' : undefined)}
        className={twMerge(`form-control h-12 w-full rounded-form border bg-white px-4 text-sm text-mis-ink transition placeholder:text-mis-muted focus:border-mis-blue focus:shadow-input focus:outline-none disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500 ${
          error ? 'border-red-400' : 'border-mis-border'
        } ${className}`)}
        {...props}
      />
      {error ? (
        <p className="text-sm text-red-600" id={errorId}>
          {error}
        </p>
      ) : hint ? <p className="text-sm text-slate-500">{hint}</p> : null}
    </div>
  );
}
