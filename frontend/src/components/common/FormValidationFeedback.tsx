import { useEffect, useRef, type ReactNode } from 'react';
import { useLocalization } from '../../context/LocalizationContext';
import { useToast } from './Toast';

type Control = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;

function fieldLabel(control: Control) {
  const label = control.labels?.[0] ?? control.closest('label');
  const copy = label?.cloneNode(true) as HTMLElement | undefined;
  copy?.querySelectorAll('input, select, textarea, button, [aria-hidden="true"]').forEach(node => node.remove());
  return (control.getAttribute('aria-label') || copy?.textContent || control.getAttribute('placeholder') || '')
    .replace(/\s+/g, ' ').trim().slice(0, 100);
}

function validationMessage(control: Control, arabic: boolean) {
  const validity = control.validity;
  if (validity.customError) return control.validationMessage;
  if (validity.valueMissing) return arabic ? 'هذا الحقل مطلوب.' : 'This field is required.';
  if (validity.typeMismatch) return control instanceof HTMLInputElement && control.type === 'email'
    ? (arabic ? 'أدخل بريدًا إلكترونيًا صحيحًا.' : 'Enter a valid email address.')
    : (arabic ? 'أدخل قيمة بالصيغة المطلوبة.' : 'Enter a value in the required format.');
  if (validity.rangeUnderflow) return arabic ? `القيمة يجب ألا تقل عن ${control.getAttribute('min')}.` : `The value must be at least ${control.getAttribute('min')}.`;
  if (validity.rangeOverflow) return arabic ? `القيمة يجب ألا تتجاوز ${control.getAttribute('max')}.` : `The value must not exceed ${control.getAttribute('max')}.`;
  if (validity.tooLong) return arabic ? 'القيمة أطول من الحد المسموح.' : 'The value exceeds the allowed length.';
  if (validity.tooShort) return arabic ? 'القيمة أقصر من الحد المطلوب.' : 'The value is shorter than required.';
  if (validity.badInput || validity.stepMismatch) return arabic ? 'أدخل رقمًا صحيحًا وفق القيم المسموحة.' : 'Enter a number that matches the allowed values.';
  return arabic ? 'راجع قيمة هذا الحقل والصيغة المطلوبة.' : 'Check this field and its required format.';
}

/** Retain HTML constraint validation, but render feedback through MIS instead of browser bubbles. */
export function FormValidationFeedback({ children }: { children: ReactNode }) {
  const { language } = useLocalization();
  const toast = useToast();
  const pending = useRef<Control | null>(null);

  useEffect(() => {
    const invalid = (event: Event) => {
      const control = event.target;
      if (!(control instanceof HTMLInputElement || control instanceof HTMLSelectElement || control instanceof HTMLTextAreaElement)) return;
      event.preventDefault();
      const hidden = control.getAttribute('aria-hidden') === 'true' || control.tabIndex < 0 || control.classList.contains('sr-only');
      if (!hidden) control.dataset.systemInvalid = 'true';
      // One submission can emit invalid events for many fields. Show the first
      // error once while allowing composite controls' own onInvalid handlers.
      if (pending.current) return;
      pending.current = control;
      queueMicrotask(() => {
        pending.current = null;
        if (!control.isConnected || control.validity.valid) return;
        const label = fieldLabel(control);
        toast.error(`${label ? `${label}: ` : ''}${validationMessage(control, language === 'ar')}`);
        if (!hidden) control.focus();
      });
    };
    const clear = (event: Event) => {
      const control = event.target;
      if ((control instanceof HTMLInputElement || control instanceof HTMLSelectElement || control instanceof HTMLTextAreaElement) && control.validity.valid)
        delete control.dataset.systemInvalid;
    };
    const reset = (event: Event) => {
      if (event.target instanceof HTMLFormElement)
        event.target.querySelectorAll<HTMLElement>('[data-system-invalid]').forEach(control => delete control.dataset.systemInvalid);
    };
    document.addEventListener('invalid', invalid, true);
    document.addEventListener('input', clear, true);
    document.addEventListener('change', clear, true);
    document.addEventListener('reset', reset, true);
    return () => {
      document.removeEventListener('invalid', invalid, true);
      document.removeEventListener('input', clear, true);
      document.removeEventListener('change', clear, true);
      document.removeEventListener('reset', reset, true);
    };
  }, [language, toast]);

  return children;
}
