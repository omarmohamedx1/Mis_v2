import type { ChangeEvent, InputHTMLAttributes } from 'react';
import { ProfessionalDateInput } from './ProfessionalDateInput';

export interface DateControlProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  mode?: 'date' | 'datetime';
}

export function DateControl({ className = '', defaultValue, disabled, id, max, min, mode = 'date', name, onChange, required, value, ...props }: DateControlProps) {
  // Legacy pages used visual input classes directly on native date fields. Keep
  // only layout classes here so the shared control always has one clean border.
  const layoutClassName = className
    .split(/\s+/)
    .filter(Boolean)
    .filter((token) => !/^(field|!?h-|min-h-|w-full|rounded|border|bg-white|!?p[xyse]?[-:]|text-(xs|sm)|leading-|focus:|disabled:)/.test(token))
    .join(' ');
  const notify = (next: string) => {
    const target = { name: name ?? '', value: next } as HTMLInputElement;
    onChange?.({ target, currentTarget: target } as ChangeEvent<HTMLInputElement>);
  };
  return <ProfessionalDateInput ariaLabel={props['aria-label']} className={layoutClassName} defaultValue={typeof defaultValue === 'string' ? defaultValue : undefined} disabled={disabled} id={id} max={typeof max === 'string' ? max : undefined} min={typeof min === 'string' ? min : undefined} mode={mode} name={name} onChange={notify} required={required} value={typeof value === 'string' ? value : undefined} />;
}
