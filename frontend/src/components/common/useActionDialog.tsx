import { useEffect, useId, useRef, useState } from 'react';
import { useLocalization } from '../../context/LocalizationContext';
import { formControlClass } from '../forms/FormField';
import { Button } from './Button';
import { ConfirmDialog } from './ConfirmDialog';
import { Modal } from './Modal';

type Request = { kind: 'prompt' | 'confirm'; message: string; value: string };

/** Themed replacements for native prompts; callers keep their existing checks. */
export function useActionDialog() {
  const { language, t } = useLocalization();
  const [request, setRequest] = useState<Request | null>(null);
  const resolve = useRef<((value: string | boolean | null) => void) | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const formId = useId();
  const title = language === 'ar' ? 'تأكيد الإجراء' : 'Confirm action';
  const finish = (value: string | boolean | null) => {
    const pending = resolve.current;
    resolve.current = null;
    setRequest(null);
    pending?.(value);
  };
  useEffect(() => () => { resolve.current?.(null); resolve.current = null; }, []);
  const ask = (kind: Request['kind'], message: string, value = '') => new Promise<string | boolean | null>(done => {
    if (resolve.current) { done(null); return; }
    resolve.current = done;
    setRequest({ kind, message, value });
  });
  const dialog = request?.kind === 'confirm'
    ? <ConfirmDialog open title={title} message={request.message} onCancel={() => finish(false)} onConfirm={() => finish(true)} />
    : request ? <Modal open title={title} size="sm" initialFocusRef={inputRef} onClose={() => finish(null)} footer={<>
      <Button fullWidth={false} variant="outline" type="button" onClick={() => finish(null)}>{t('cancel')}</Button>
      <Button fullWidth={false} type="submit" form={formId}>{t('confirm')}</Button>
    </>}><form id={formId} className="min-w-0 space-y-2" onSubmit={event => { event.preventDefault(); finish(request.value); }}>
      <label className="block text-sm font-semibold [overflow-wrap:anywhere]" htmlFor={`${formId}-value`}>{request.message}</label>
      <input id={`${formId}-value`} ref={inputRef} className={`${formControlClass} h-12 px-4`} value={request.value} onChange={event => setRequest({ ...request, value: event.target.value })} />
    </form></Modal> : null;
  return {
    prompt: async (message: string, defaultValue = '') => { const value = await ask('prompt', message, defaultValue); return typeof value === 'string' ? value : null; },
    confirm: async (message: string) => (await ask('confirm', message)) === true,
    dialog,
  };
}
