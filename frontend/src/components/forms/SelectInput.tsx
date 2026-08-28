import { forwardRef, useId, type ReactNode, type SelectHTMLAttributes } from 'react';
import { FormField, formControlClass, joinDescribedBy } from './FormField';
import { ProfessionalSelect } from './ProfessionalSelect';

export interface SelectInputProps extends SelectHTMLAttributes<HTMLSelectElement> {
  containerClassName?: string;
  error?: ReactNode;
  hint?: ReactNode;
  label: ReactNode;
}

export const SelectInput = forwardRef<HTMLSelectElement, SelectInputProps>(function SelectInput(
  {
    'aria-describedby': ariaDescribedBy,
    children,
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
      <ProfessionalSelect
        aria-describedby={joinDescribedBy(ariaDescribedBy, hintId, errorId)}
        aria-invalid={error ? true : undefined}
        className={`h-12 px-4 ${formControlClass} ${error ? 'border-red-400' : 'border-mis-border'} ${className}`}
        id={inputId}
        name={name}
        ref={ref}
        required={required}
        {...props}
      >
        {children}
      </ProfessionalSelect>
    </FormField>
  );
});
