import { twMerge } from 'tailwind-merge';
import { forwardRef, useId, useState, type InputHTMLAttributes, type ReactNode } from 'react';
import { useLocalization } from '../../context/LocalizationContext';
import { FormField, formControlClass, joinDescribedBy } from './FormField';

export interface FileInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'defaultValue' | 'type' | 'value'> {
  containerClassName?: string;
  error?: ReactNode;
  hint?: ReactNode;
  label: ReactNode;
}

export const FileInput = forwardRef<HTMLInputElement, FileInputProps>(function FileInput(
  {
    'aria-describedby': ariaDescribedBy,
    className = '',
    containerClassName = '',
    error,
    hint,
    id,
    label,
    name,
    disabled,
    onChange,
    required,
    ...props
  },
  ref,
) {
  const { t } = useLocalization();
  const generatedId = useId();
  const [fileName, setFileName] = useState('');
  const inputId = id ?? name ?? generatedId;
  const errorId = error ? `${inputId}-error` : undefined;
  const hintId = hint ? `${inputId}-hint` : undefined;

  return (
    <FormField className={containerClassName} error={error} errorId={errorId} hint={hint} hintId={hintId} inputId={inputId} label={label} required={required}>
      <div data-invalid={Boolean(error) || undefined} className={twMerge(`relative flex min-h-12 focus-within:border-mis-blue focus-within:shadow-input items-center gap-3 overflow-hidden px-3 py-1 ${formControlClass} ${error ? 'border-red-500' : 'border-mis-control'} ${disabled ? 'cursor-not-allowed bg-slate-100 text-slate-500' : 'cursor-pointer'} ${className}`)}>
        <span className="flex-none rounded-lg bg-mis-pale px-3 py-2 text-sm font-semibold text-mis-deep">{t('chooseFile')}</span>
        <span className="min-w-0 flex-1 truncate text-sm text-slate-500" dir="auto" title={fileName || undefined}>{fileName || t('noFileChosen')}</span>
        <input
          aria-describedby={joinDescribedBy(ariaDescribedBy, hintId, errorId)}
          aria-invalid={error ? true : undefined}
          className="absolute inset-0 h-full w-full cursor-pointer opacity-0 disabled:cursor-not-allowed"
          disabled={disabled}
          id={inputId}
          name={name}
          onChange={(event) => {
            setFileName(event.target.files?.[0]?.name ?? '');
            onChange?.(event);
          }}
          ref={ref}
          required={required}
          type="file"
          {...props}
        />
      </div>
    </FormField>
  );
});
