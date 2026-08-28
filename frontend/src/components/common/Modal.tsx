import { X } from 'lucide-react';
import { useEffect, useId, useRef, type ReactNode, type RefObject } from 'react';
import { createPortal } from 'react-dom';
import { useLocalization } from '../../context/LocalizationContext';

export type ModalSize = 'sm' | 'md' | 'lg' | 'xl' | 'full';

export interface ModalProps {
  bodyClassName?: string;
  children: ReactNode;
  className?: string;
  closeLabel?: string;
  closeOnBackdrop?: boolean;
  closeOnEscape?: boolean;
  description?: ReactNode;
  dialogRole?: 'dialog' | 'alertdialog';
  footer?: ReactNode;
  hideCloseButton?: boolean;
  initialFocusRef?: RefObject<HTMLElement | null>;
  onClose: () => void;
  open: boolean;
  size?: ModalSize;
  title: ReactNode;
}

const sizeClasses: Record<ModalSize, string> = {
  sm: 'max-w-md',
  md: 'max-w-xl',
  lg: 'max-w-3xl',
  xl: 'max-w-5xl',
  full: 'max-w-[calc(100vw-2rem)]',
};

const focusableSelector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>(focusableSelector)).filter((element) => element.tabIndex >= 0 && !element.hasAttribute('hidden'));
}

export function Modal({
  bodyClassName = 'p-4 sm:p-6',
  children,
  className = '',
  closeLabel,
  closeOnBackdrop = true,
  closeOnEscape = true,
  description,
  dialogRole = 'dialog',
  footer,
  hideCloseButton = false,
  initialFocusRef,
  onClose,
  open,
  size = 'md',
  title,
}: ModalProps) {
  const { t } = useLocalization();
  const dialogRef = useRef<HTMLDivElement>(null);
  const onCloseRef = useRef(onClose);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    if (!open) return undefined;

    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const previousOverflow = document.body.style.overflow;
    const dialog = dialogRef.current;
    document.body.style.overflow = 'hidden';

    const frame = window.requestAnimationFrame(() => {
      const target = initialFocusRef?.current ?? (dialog ? getFocusableElements(dialog)[0] : null) ?? dialog;
      target?.focus();
    });

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && closeOnEscape) {
        event.preventDefault();
        onCloseRef.current();
        return;
      }

      if (event.key !== 'Tab' || !dialog) return;
      const focusable = getFocusableElements(dialog);

      if (focusable.length === 0) {
        event.preventDefault();
        dialog.focus();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const activeElement = document.activeElement;

      if (event.shiftKey && (activeElement === first || !dialog.contains(activeElement))) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', handleKeyDown, true);

    return () => {
      window.cancelAnimationFrame(frame);
      document.removeEventListener('keydown', handleKeyDown, true);
      document.body.style.overflow = previousOverflow;
      if (previouslyFocused && document.contains(previouslyFocused)) previouslyFocused.focus();
    };
  }, [closeOnEscape, initialFocusRef, open]);

  if (!open) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-mis-ink/45 p-2 backdrop-blur-[2px] sm:p-4"
      onMouseDown={(event) => {
        if (closeOnBackdrop && event.target === event.currentTarget) onCloseRef.current();
      }}
    >
      <div
        aria-describedby={description ? descriptionId : undefined}
        aria-labelledby={titleId}
        aria-modal="true"
        className={`my-auto flex max-h-[calc(100dvh-1rem)] w-full flex-col overflow-hidden rounded-2xl border border-mis-border bg-white shadow-panel sm:max-h-[calc(100dvh-2rem)] ${sizeClasses[size]} ${className}`}
        ref={dialogRef}
        role={dialogRole}
        tabIndex={-1}
      >
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-mis-border px-4 py-4 sm:px-6 sm:py-5">
          <div className="min-w-0">
            <h2 className="break-words text-xl font-bold text-mis-navy" id={titleId}>{title}</h2>
            {description ? <div className="mt-1 text-sm leading-5 text-slate-500" id={descriptionId}>{description}</div> : null}
          </div>
          {!hideCloseButton ? (
            <button
              aria-label={closeLabel ?? t('close')}
              className="flex-none rounded-lg p-2 text-slate-500 transition hover:bg-slate-50 hover:text-mis-primary"
              onClick={() => onCloseRef.current()}
              type="button"
            >
              <X className="h-5 w-5" aria-hidden="true" />
            </button>
          ) : null}
        </header>
        <div className={`min-h-0 flex-1 overflow-y-auto ${bodyClassName}`}>{children}</div>
        {footer ? <footer className="flex shrink-0 flex-col-reverse gap-2 border-t border-mis-border px-4 py-3 [&>*]:w-full sm:flex-row sm:flex-wrap sm:items-center sm:justify-end sm:gap-3 sm:px-6 sm:py-4 sm:[&>*]:w-auto">{footer}</footer> : null}
      </div>
    </div>,
    document.body,
  );
}
