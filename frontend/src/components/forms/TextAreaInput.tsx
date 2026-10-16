import { twMerge } from 'tailwind-merge';
import { forwardRef, useId, type ReactNode, type TextareaHTMLAttributes } from 'react';
import { FormField, formControlClass, joinDescribedBy } from './FormField';

export interface TextAreaInputProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  containerClassName?: string;
  error?: ReactNode;
  hint?: ReactNode;
  label: ReactNode;
}

export const TextAreaInput = forwardRef<HTMLTextAreaElement, TextAreaInputProps>(function TextAreaInput(
  {
    'aria-describedby': ariaDescribedBy,
    className = '',
    containerClassName = '',
    error,
    hint,
    id,
    label,
    name,
    required,
    ...props
  },
  ref,
) {
  const generatedId = useId();
  const inputId = id ?? name ?? generatedId;
  const errorId = error ? `${inputId}-error` : undefined;
  const hintId = hint ? `${inputId}-hint` : undefined;

  return (
    <FormField className={containerClassName} error={error} errorId={errorId} hint={hint} hintId={hintId} inputId={inputId} label={label} required={required}>
      <textarea
        aria-describedby={joinDescribedBy(ariaDescribedBy, hintId, errorId)}
        aria-invalid={error ? true : undefined}
        className={twMerge(`min-h-28 resize-y px-4 py-3 ${formControlClass} ${error ? 'border-red-500' : 'border-mis-control'} ${className}`)}
        id={inputId}
        name={name}
        ref={ref}
        required={required}
        {...props}
      />
    </FormField>
  );
});
