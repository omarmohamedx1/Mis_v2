import { forwardRef, useId, type ChangeEvent, type InputHTMLAttributes, type ReactNode } from 'react';
import { FormField } from './FormField';
import { ProfessionalTimeInput } from './ProfessionalTimeInput';

export interface TimeInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  containerClassName?: string;
  error?: ReactNode;
  hint?: ReactNode;
  label: ReactNode;
}

export const TimeInput = forwardRef<HTMLInputElement, TimeInputProps>(function TimeInput(
  { className = '', containerClassName = '', defaultValue, disabled, error, hint, id, label, name, onChange, required, value },
  ref,
) {
  void ref;
  const generatedId = useId();
  const inputId = id ?? name ?? generatedId;
  const errorId = error ? `${inputId}-error` : undefined;
  const hintId = hint ? `${inputId}-hint` : undefined;
  const notify = (next: string) => {
    const target = { name: name ?? '', value: next } as HTMLInputElement;
    onChange?.({ target, currentTarget: target } as ChangeEvent<HTMLInputElement>);
  };

  return (
    <FormField className={containerClassName} error={error} errorId={errorId} hint={hint} hintId={hintId} inputId={inputId} label={label} required={required}>
      <ProfessionalTimeInput
        className={className}
        defaultValue={typeof defaultValue === 'string' ? defaultValue : undefined}
        disabled={disabled}
        id={inputId}
        name={name}
        onChange={notify}
        required={required}
        value={typeof value === 'string' ? value : undefined}
      />
    </FormField>
  );
});
