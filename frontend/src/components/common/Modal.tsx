import { X } from 'lucide-react';
import { useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode, type RefObject } from 'react';
import { createPortal } from 'react-dom';
import { useLocalization } from '../../context/LocalizationContext';
import { OverlayScope, useBodyScrollLock, useFocusBoundary, useOverlayLayer, useOverlayOwners } from './overlay';

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
  hideHeader?: boolean;
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
  hideHeader = false,
  initialFocusRef,
  onClose,
  open,
  size = 'md',
  title,
}: ModalProps) {
  const { t } = useLocalization();
  const dialogRef = useRef<HTMLDivElement>(null);
  const headerRef = useRef<HTMLElement>(null);
  const footerRef = useRef<HTMLElement>(null);
  const [scrollWholeDialog, setScrollWholeDialog] = useState(false);
  const onCloseRef = useRef(onClose);
  const titleId = useId();
  const descriptionId = useId();
  const overlayId = useId(); const owners = useOverlayOwners(overlayId);
  useBodyScrollLock(open);
  useOverlayLayer(overlayId, open, () => { if (closeOnEscape) onCloseRef.current(); });

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useFocusBoundary(overlayId, open, dialogRef, initialFocusRef);

  useLayoutEffect(() => {
    if (!open || !dialogRef.current) return;
    const measure = () => {
      const dialog = dialogRef.current;
      if (!dialog) return;
      const chromeHeight = (headerRef.current?.scrollHeight ?? 0) + (footerRef.current?.scrollHeight ?? 0);
      const tokens = getComputedStyle(document.documentElement);
      const controlHeight = parseFloat(tokens.getPropertyValue('--control-height')) * parseFloat(tokens.fontSize);
      setScrollWholeDialog(chromeHeight + controlHeight > dialog.clientHeight);
    };
    measure();
    const observer = new ResizeObserver(measure);
    for (const element of [dialogRef.current, headerRef.current, footerRef.current]) if (element) observer.observe(element);
    return () => observer.disconnect();
  }, [open, hideHeader]);

  if (!open) return null;

  return <OverlayScope owners={owners}>{createPortal(
    <div
      data-overlay-owners={owners.join(' ')} className="dialog-backdrop fixed inset-0 z-[var(--z-dialog)] flex items-center justify-center overflow-y-auto bg-mis-ink/45 p-2 backdrop-blur-[2px] sm:p-4"
      onMouseDown={(event) => {
        if (closeOnBackdrop && event.target === event.currentTarget) onCloseRef.current();
      }}
    >
      <div
        aria-describedby={description ? descriptionId : undefined}
        aria-labelledby={titleId}
        aria-modal="true"
        data-scroll-whole={scrollWholeDialog || undefined}
        className={`my-auto flex max-h-[calc(100dvh-1rem)] w-full flex-col overflow-hidden rounded-2xl border border-mis-border bg-white shadow-panel sm:max-h-[calc(100dvh-2rem)] ${sizeClasses[size]} ${className}`}
        ref={dialogRef}
        role={dialogRole}
        tabIndex={-1}
      >
        {hideHeader ? <h2 className="sr-only" id={titleId}>{title}</h2> : <header ref={headerRef} className="flex shrink-0 items-start justify-between gap-4 border-b border-mis-border px-4 py-4 sm:px-6 sm:py-5 [overflow-wrap:anywhere]">
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
        </header>}
        <div className={`min-h-0 min-w-0 ${scrollWholeDialog ? 'flex-none' : 'flex-1 overflow-y-auto'} [overflow-wrap:anywhere] ${bodyClassName}`}>{children}</div>
        {footer ? <footer ref={footerRef} className="flex shrink-0 flex-col-reverse gap-2 border-t border-mis-border px-4 py-3 [&>*]:w-full sm:flex-row sm:flex-wrap sm:items-center sm:justify-end sm:gap-3 sm:px-6 sm:py-4 sm:[&>*]:w-auto">{footer}</footer> : null}
      </div>
    </div>,
    document.body,
  )}</OverlayScope>;
}
