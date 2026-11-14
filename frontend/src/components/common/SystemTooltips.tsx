import { useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useLocation } from 'react-router-dom';
import { viewportBounds } from './overlay';

/** Shared, non-interactive help for data-tooltip triggers throughout the app. */
export function SystemTooltips({ children }: { children: ReactNode }) {
  const id = useId();
  const location = useLocation();
  const panel = useRef<HTMLDivElement>(null);
  const [tooltip, setTooltip] = useState<{ target: HTMLElement; text: string }>();
  const [position, setPosition] = useState({ left: 0, top: -1000 });

  useEffect(() => { setTooltip(undefined); }, [location.key]);

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    let target: HTMLElement | null = null;
    const hide = () => { clearTimeout(timer); target = null; setTooltip(undefined); };
    const show = (element: HTMLElement | null, delay: number) => {
      if (!element || !element.dataset.tooltip?.trim() || element.getAttribute('aria-expanded') === 'true') { hide(); return; }
      if (target === element) return;
      hide();
      target = element;
      timer = setTimeout(() => {
        if (element.isConnected && element.getAttribute('aria-expanded') !== 'true') {
          setPosition({ left: 0, top: -1000 });
          setTooltip({ target: element, text: element.dataset.tooltip! });
        }
      }, delay);
    };
    const over = (event: PointerEvent) => {
      if (event.pointerType !== 'touch') show(event.target instanceof Element ? event.target.closest<HTMLElement>('[data-tooltip]') : null, 500);
    };
    const focus = (event: FocusEvent) => {
      if (event.target instanceof HTMLElement && event.target.matches(':focus-visible')) show(event.target.closest<HTMLElement>('[data-tooltip]'), 250);
    };
    const out = (event: MouseEvent | FocusEvent) => {
      if (!(event.relatedTarget instanceof Node) || !target?.contains(event.relatedTarget)) hide();
    };
    const key = (event: KeyboardEvent) => { if (event.key === 'Escape') hide(); };
    document.addEventListener('pointerover', over, true);
    document.addEventListener('pointerout', out, true);
    document.addEventListener('focusin', focus, true);
    document.addEventListener('focusout', out, true);
    document.addEventListener('pointerdown', hide, true);
    document.addEventListener('keydown', key, true);
    window.addEventListener('scroll', hide, true);
    window.addEventListener('resize', hide);
    return () => {
      clearTimeout(timer);
      document.removeEventListener('pointerover', over, true);
      document.removeEventListener('pointerout', out, true);
      document.removeEventListener('focusin', focus, true);
      document.removeEventListener('focusout', out, true);
      document.removeEventListener('pointerdown', hide, true);
      document.removeEventListener('keydown', key, true);
      window.removeEventListener('scroll', hide, true);
      window.removeEventListener('resize', hide);
    };
  }, []);

  useLayoutEffect(() => {
    if (!tooltip || !panel.current) return;
    const { target } = tooltip;
    const anchor = target.getBoundingClientRect();
    const box = panel.current.getBoundingClientRect();
    const bounds = viewportBounds(12);
    const top = anchor.top - box.height - 8;
    setPosition({
      left: Math.max(bounds.left, Math.min(anchor.left + (anchor.width - box.width) / 2, bounds.right - box.width)),
      top: Math.max(bounds.top, Math.min(top >= bounds.top ? top : anchor.bottom + 8, bounds.bottom - box.height)),
    });
    const descriptions = new Set((target.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean));
    descriptions.add(id);
    target.setAttribute('aria-describedby', [...descriptions].join(' '));
    return () => {
      const remaining = (target.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(value => value && value !== id);
      if (remaining.length) target.setAttribute('aria-describedby', remaining.join(' '));
      else target.removeAttribute('aria-describedby');
    };
  }, [id, tooltip]);

  return <>{children}{tooltip ? createPortal(
    <div ref={panel} id={id} role="tooltip" data-overlay-owners={tooltip.target.closest('[data-overlay-owners]')?.getAttribute('data-overlay-owners') ?? undefined}
      className="pointer-events-none fixed z-[var(--z-tooltip)] max-w-[min(20rem,calc(100vw-1.5rem))] rounded-xl border border-slate-700 bg-mis-navy px-3 py-2 text-sm leading-6 text-white shadow-panel [overflow-wrap:anywhere]"
      dir="auto" style={position}>{tooltip.text}</div>, document.body,
  ) : null}</>;
}
