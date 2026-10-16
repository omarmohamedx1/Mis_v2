import { useId, type InputHTMLAttributes } from 'react';

interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: string;
}

export function Checkbox({ id, label, className = '', ...props }: CheckboxProps) {
  const generatedId = useId();
  const inputId = id ?? props.name ?? generatedId;

  return (
    <label className="inline-flex min-h-6 min-w-0 cursor-pointer items-center gap-2 text-sm font-medium text-slate-600" htmlFor={inputId}>
      <input
        id={inputId}
        type="checkbox"
        className={`h-4 w-4 shrink-0 rounded border-mis-control text-mis-primary focus:ring-mis-blue ${className}`}
        {...props}
      />
      <span className="min-w-0 [overflow-wrap:anywhere]">{label}</span>
    </label>
  );
}
