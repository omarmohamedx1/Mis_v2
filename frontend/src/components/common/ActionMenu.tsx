import { EllipsisVertical } from 'lucide-react';
import { Children, cloneElement, isValidElement, useEffect, useId, useLayoutEffect, useRef, useState, type KeyboardEvent, type ReactElement, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { twMerge } from 'tailwind-merge';
import { useLocalization } from '../../context/LocalizationContext';
import { Button } from './Button';
import { OverlayScope, isTopOverlay, useFloatingPanel, useOverlayLayer, useOverlayOwners } from './overlay';

export const actionMenuItemClasses = 'flex h-auto min-h-11 w-full max-w-full items-center justify-start gap-2.5 whitespace-normal rounded-lg border-0 bg-transparent px-3 py-2 text-start text-sm font-semibold leading-relaxed text-slate-600 shadow-none hover:bg-mis-pale hover:text-mis-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-mis-primary disabled:cursor-not-allowed disabled:opacity-50 [&_svg]:size-4 [&_svg]:shrink-0';

type ActionProps = { children?: ReactNode; className?: string; title?: string; 'aria-label'?: string; variant?: string; size?: string; fullWidth?: boolean; role?: string; tabIndex?: number };

function textContent(node: ReactNode): string {
  return Children.toArray(node).map(child => {
    if (typeof child === 'string' || typeof child === 'number') return String(child);
    return isValidElement<ActionProps>(child) ? textContent(child.props.children) : '';
  }).join('').trim();
}

// Keep the existing links, handlers and permission conditions in their owning page.
function menuChildren(children: ReactNode): ReactNode {
  return Children.map(children, child => {
    if (!isValidElement<ActionProps>(child)) return child;
    const { className = '', variant, children: content } = child.props;
    if (child.type === 'button' || child.type === 'a' || child.type === Link || child.type === Button) {
      const danger = variant === 'danger' || /(?:text|bg)-(?:red|rose)-/.test(className);
      const success = /text-emerald-/.test(className);
      const warning = /text-amber-/.test(className);
      const tone = danger ? 'text-red-600 hover:bg-red-50 hover:text-red-700' : success ? 'text-emerald-700 hover:bg-emerald-50 hover:text-emerald-800' : warning ? 'text-amber-700 hover:bg-amber-50 hover:text-amber-800' : '';
      const label = child.props.title || child.props['aria-label'];
      return cloneElement(child as ReactElement<ActionProps>, {
        className: twMerge(className, actionMenuItemClasses, tone),
        role: 'menuitem',
        tabIndex: -1,
        ...(child.type === Button ? { variant: 'ghost', size: 'sm', fullWidth: true } : {}),
        ...(child.type === 'button' || child.type === Button ? { type: 'button' } : {}),
      }, content, !textContent(content) && label ? <span>{label}</span> : null);
    }
    return content === undefined ? child : cloneElement(child, {}, menuChildren(content));
  });
}

function hasActions(children: ReactNode): boolean {
  return Children.toArray(children).some(child => isValidElement<ActionProps>(child) && (
    child.type === 'button' || child.type === 'a' || child.type === Link || child.type === Button
    || (child.props.children === undefined && typeof child.type !== 'string') || hasActions(child.props.children)
  ));
}

/** A compact row menu. Its actions stay mounted so their confirmation dialogs retain state. */
export function ActionMenu({ children, label, className = '' }: { children: ReactNode; label?: string; className?: string }) {
  const { language } = useLocalization();
  const id = useId();
  const menuId = `actions-${id}`;
  const owners = useOverlayOwners(menuId);
  const trigger = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLDivElement>(null);
  const initialFocus = useRef<'first' | 'last'>('first');
  const [open, setOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [direction, setDirection] = useState<'rtl' | 'ltr'>(language === 'ar' ? 'rtl' : 'ltr');
  const position = useFloatingPanel(open, trigger, panel, 224);
  const title = label ?? (language === 'ar' ? 'الإجراءات' : 'Actions');
  const close = (restoreFocus = true) => {
    setOpen(false);
    if (restoreFocus) trigger.current?.focus();
  };
  useOverlayLayer(menuId, open, () => close());

  const items = () => [...(panel.current?.querySelectorAll<HTMLElement>('[role="menuitem"]:not(:disabled):not([aria-disabled="true"])') ?? [])].filter(item => item.getClientRects().length > 0);
  useLayoutEffect(() => {
    if (!open) return;
    setDirection(getComputedStyle(trigger.current!).direction === 'rtl' ? 'rtl' : 'ltr');
    const available = items();
    (initialFocus.current === 'last' ? available.at(-1) : available[0])?.focus();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const dismissOutside = (event: Event) => {
      if (!isTopOverlay(menuId) || !(event.target instanceof Node)) return;
      if (!trigger.current?.contains(event.target) && !panel.current?.contains(event.target)) close(false);
    };
    document.addEventListener('pointerdown', dismissOutside, true);
    document.addEventListener('focusin', dismissOutside);
    return () => {
      document.removeEventListener('pointerdown', dismissOutside, true);
      document.removeEventListener('focusin', dismissOutside);
    };
  }, [menuId, open]);

  const show = (edge: 'first' | 'last' = 'first') => {
    initialFocus.current = edge;
    setMounted(true);
    setOpen(true);
  };
  const navigate = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Tab') { close(); return; }
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    event.stopPropagation();
    const available = items();
    if (!available.length) return;
    const index = available.indexOf(document.activeElement as HTMLElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? available.length - 1 : (index + (event.key === 'ArrowDown' ? 1 : -1) + available.length) % available.length;
    available[next]?.focus();
  };

  if (!hasActions(children)) return null;
  return <>
    <button ref={trigger} type="button" aria-label={title} title={title} aria-haspopup="menu" aria-expanded={open} aria-controls={mounted ? menuId : undefined}
      className={twMerge('inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-mis-border bg-white text-slate-600 transition hover:border-mis-primary hover:bg-mis-pale hover:text-mis-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-mis-primary aria-expanded:border-mis-primary aria-expanded:bg-mis-pale aria-expanded:text-mis-primary', className)}
      onClick={event => { event.stopPropagation(); if (open) close(); else show(); }}
      onKeyDown={event => { if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); event.stopPropagation(); show(event.key === 'ArrowUp' ? 'last' : 'first'); } }}>
      <EllipsisVertical className="h-5 w-5" aria-hidden="true" />
    </button>
    {mounted && createPortal(<OverlayScope owners={owners}>
      <div ref={panel} id={menuId} role="menu" aria-label={title} aria-orientation="vertical" dir={direction} data-overlay-owners={owners.join(' ')}
        className="fixed z-[var(--z-select)] overflow-y-auto rounded-xl border border-mis-border bg-white p-1.5 shadow-xl"
        style={{ ...position, display: open ? undefined : 'none' }} onKeyDown={navigate}
        onClickCapture={event => {
          const action = event.target instanceof Element ? event.target.closest('[role="menuitem"]') : null;
          if (action && !action.matches(':disabled,[aria-disabled="true"]')) close();
        }}
        onClick={event => event.stopPropagation()}>
        {menuChildren(children)}
      </div>
    </OverlayScope>, document.body)}
  </>;
}
