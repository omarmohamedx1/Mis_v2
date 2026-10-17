import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-react';
import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useLocalization } from '../../context/LocalizationContext';
import { viewportBounds } from './overlay';

export type ToastVariant = 'success' | 'error' | 'warning' | 'info';

export interface ToastAction {
  dismissOnClick?: boolean;
  label: ReactNode;
  onClick: () => void;
}

export interface ToastOptions {
  action?: ToastAction;
  duration?: number;
  message: ReactNode;
  title?: ReactNode;
  variant?: ToastVariant;
}

interface ToastRecord extends ToastOptions {
  duration: number;
  id: string;
  variant: ToastVariant;
}

type ToastShortcutOptions = Omit<ToastOptions, 'message' | 'variant'>;

export interface ToastContextValue {
  clearToasts: () => void;
  dismissToast: (id: string) => void;
  error: (message: ReactNode, options?: ToastShortcutOptions) => string;
  info: (message: ReactNode, options?: ToastShortcutOptions) => string;
  showToast: (options: ToastOptions) => string;
  success: (message: ReactNode, options?: ToastShortcutOptions) => string;
  warning: (message: ReactNode, options?: ToastShortcutOptions) => string;
}

export interface ToastProviderProps {
  children: ReactNode;
  defaultDuration?: number;
  dismissLabel?: string;
  maxToasts?: number;
}

const ToastContext = createContext<ToastContextValue | null>(null);
let toastSequence = 0;

function createToastId(): string {
  toastSequence += 1;
  return `mis-toast-${Date.now()}-${toastSequence}`;
}

const toastStyles: Record<ToastVariant, { container: string; icon: ReactNode }> = {
  success: { container: 'border-emerald-200 bg-emerald-50 text-emerald-800', icon: <CheckCircle2 className="h-5 w-5" aria-hidden="true" /> },
  error: { container: 'border-red-200 bg-red-50 text-red-800', icon: <XCircle className="h-5 w-5" aria-hidden="true" /> },
  warning: { container: 'border-amber-200 bg-amber-50 text-amber-800', icon: <AlertTriangle className="h-5 w-5" aria-hidden="true" /> },
  info: { container: 'border-mis-sky/60 bg-mis-pale text-mis-deep', icon: <Info className="h-5 w-5" aria-hidden="true" /> },
};

function ToastItem({ dismissLabel, dismissToast, toast }: { dismissLabel: string; dismissToast: (id: string) => void; toast: ToastRecord }) {
  useEffect(() => {
    if (toast.duration <= 0) return undefined;
    const timer = window.setTimeout(() => dismissToast(toast.id), toast.duration);
    return () => window.clearTimeout(timer);
  }, [dismissToast, toast.duration, toast.id]);

  const style = toastStyles[toast.variant];
  return (
    <article className={`pointer-events-auto w-full rounded-xl border p-4 shadow-panel ${style.container}`} role={toast.variant === 'error' ? 'alert' : 'status'}>
      <div className="flex items-start gap-3">
        <div className="mt-0.5 flex-none">{style.icon}</div>
        <div className="min-w-0 flex-1">
          {toast.title ? <p className="font-bold [overflow-wrap:anywhere]">{toast.title}</p> : null}
          <div className={`${toast.title ? 'mt-1' : ''} [overflow-wrap:anywhere] text-sm leading-5`}>{toast.message}</div>
          {toast.action ? (
            <button
              className="mt-2 text-sm font-bold underline underline-offset-2"
              onClick={() => {
                toast.action?.onClick();
                if (toast.action?.dismissOnClick !== false) dismissToast(toast.id);
              }}
              type="button"
            >
              {toast.action.label}
            </button>
          ) : null}
        </div>
        <button aria-label={dismissLabel} className="flex-none rounded-lg p-1 opacity-70 transition hover:bg-black/5 hover:opacity-100" onClick={() => dismissToast(toast.id)} type="button">
          <X className="h-4 w-4" aria-hidden="true" />
        </button>
      </div>
    </article>
  );
}

export function ToastProvider({ children, defaultDuration = 5000, dismissLabel, maxToasts = 5 }: ToastProviderProps) {
  const { t } = useLocalization();
  const [toasts, setToasts] = useState<ToastRecord[]>([]);
  const stackRef = useRef<HTMLDivElement>(null);
  const [actionClearance, setActionClearance] = useState(0);
  useLayoutEffect(() => {
    if (!toasts.length) { setActionClearance(0); return; }
    const observed = new WeakSet<Element>();
    const sizeObserver = new ResizeObserver(() => place());
    const place = () => {
      const stack = stackRef.current;
      if (!stack) return;
      const gutter = parseFloat(getComputedStyle(document.documentElement).fontSize);
      const bounds = viewportBounds(gutter);
      const stackBox = stack.getBoundingClientRect();
      let clearance = Math.max(0, innerHeight - bounds.bottom);
      const dialog = [...document.querySelectorAll<HTMLElement>('[aria-modal="true"]')].at(-1);
      const footer = dialog?.querySelector<HTMLElement>('footer');
      if (footer) {
        if (!observed.has(footer)) { observed.add(footer); sizeObserver.observe(footer); }
        const box = footer.getBoundingClientRect();
        if (box.top < bounds.bottom && box.bottom > bounds.top && stackBox.right > box.left && stackBox.left < box.right) clearance = Math.max(clearance, innerHeight - box.top + gutter / 2);
      }
      setActionClearance(previous => previous === clearance ? previous : clearance);
    };
    place();
    if (stackRef.current) sizeObserver.observe(stackRef.current);
    const mutations = new MutationObserver(place);
    mutations.observe(document.body, { childList: true, subtree: true });
    window.addEventListener('resize', place); window.addEventListener('scroll', place, true);
    window.visualViewport?.addEventListener('resize', place); window.visualViewport?.addEventListener('scroll', place);
    return () => { sizeObserver.disconnect(); mutations.disconnect(); window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true); window.visualViewport?.removeEventListener('resize', place); window.visualViewport?.removeEventListener('scroll', place); };
  }, [toasts.length]);
  const dismissToast = useCallback((id: string) => setToasts((current) => current.filter((toast) => toast.id !== id)), []);
  const clearToasts = useCallback(() => setToasts([]), []);
  const showToast = useCallback((options: ToastOptions) => {
    const toast: ToastRecord = {
      ...options,
      duration: options.duration ?? defaultDuration,
      id: createToastId(),
      variant: options.variant ?? 'info',
    };
    setToasts((current) => [...current, toast].slice(-Math.max(1, maxToasts)));
    return toast.id;
  }, [defaultDuration, maxToasts]);

  const value = useMemo<ToastContextValue>(() => ({
    clearToasts,
    dismissToast,
    error: (message, options) => showToast({ ...options, message, variant: 'error' }),
    info: (message, options) => showToast({ ...options, message, variant: 'info' }),
    showToast,
    success: (message, options) => showToast({ ...options, message, variant: 'success' }),
    warning: (message, options) => showToast({ ...options, message, variant: 'warning' }),
  }), [clearToasts, dismissToast, showToast]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div ref={stackRef} style={{ bottom: `max(1rem, var(--safe-bottom), ${actionClearance}px)`, maxHeight: `min(50dvh, max(0px, calc(100dvh - ${actionClearance}px - 1rem)))` }} aria-atomic="false" aria-live="polite" className="toast-stack pointer-events-none fixed inset-x-4 bottom-4 z-[var(--z-toast)] max-h-[50dvh] overflow-y-auto p-1.5 flex flex-col items-end gap-3 sm:start-auto sm:end-4 sm:w-full sm:max-w-sm">
        {toasts.map((toast) => <ToastItem dismissLabel={dismissLabel ?? t('dismissNotification')} dismissToast={dismissToast} key={toast.id} toast={toast} />)}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (!context) throw new Error('useToast must be used within ToastProvider.');
  return context;
}
