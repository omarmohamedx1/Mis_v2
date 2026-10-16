import { createContext, useContext, useEffect, useLayoutEffect, useRef, useState, type ReactNode, type RefObject } from 'react';

// React portals keep their ownership even though their DOM sits outside a dialog.
const OverlayOwners = createContext<readonly string[]>([]);
export function useOverlayOwners(id: string) { return [...useContext(OverlayOwners), id]; }
export function OverlayScope({ owners, children }: { owners: readonly string[]; children: ReactNode }) {
  return <OverlayOwners.Provider value={owners}>{children}</OverlayOwners.Provider>;
}
export function belongsToOverlay(target: EventTarget | null, id: string) {
  return target instanceof Element && !!target.closest(`[data-overlay-owners~="${id}"]`);
}

const layers: { id: string; dismiss: () => void }[] = [];
function escapeTop(event: KeyboardEvent) {
  if (event.key !== 'Escape' || !layers.length) return;
  event.preventDefault(); event.stopImmediatePropagation(); layers.at(-1)?.dismiss();
}
export function isTopOverlay(id: string) { return layers.at(-1)?.id === id; }
export function useOverlayLayer(id: string, open: boolean, dismiss: () => void) {
  const latest = useRef(dismiss); latest.current = dismiss;
  useEffect(() => {
    if (!open) return;
    const layer = { id, dismiss: () => latest.current() }; layers.push(layer);
    if (layers.length === 1) document.addEventListener('keydown', escapeTop, true);
    return () => {
      const index = layers.indexOf(layer); if (index >= 0) layers.splice(index, 1);
      if (!layers.length) document.removeEventListener('keydown', escapeTop, true);
    };
  }, [id, open]);
}

let scrollLocks = 0; let previousOverflow = '';
export function useBodyScrollLock(locked: boolean) {
  useEffect(() => {
    if (!locked) return;
    if (scrollLocks++ === 0) { previousOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden'; }
    return () => { if (--scrollLocks === 0) document.body.style.overflow = previousOverflow; };
  }, [locked]);
}

export function useFocusBoundary(id: string, open: boolean, container: RefObject<HTMLElement | null>, initial?: RefObject<HTMLElement | null>) {
  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusable = () => [...(container.current?.querySelectorAll<HTMLElement>('a[href],button:not([disabled]),input:not([disabled]):not([type="hidden"]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])') ?? [])]
      .filter(e => e.tabIndex >= 0 && e.getClientRects().length > 0 && getComputedStyle(e).visibility !== 'hidden' && !e.closest('[inert]'));
    const frame = requestAnimationFrame(() => (initial?.current ?? focusable()[0] ?? container.current)?.focus());
    const trap = (event: KeyboardEvent) => {
      if (event.key !== 'Tab' || !isTopOverlay(id)) return;
      const items = focusable(); const active = document.activeElement;
      if (!items.length) { event.preventDefault(); container.current?.focus(); return; }
      if (event.shiftKey && (active === items[0] || (!container.current?.contains(active) && !belongsToOverlay(active, id)))) { event.preventDefault(); items.at(-1)?.focus(); }
      else if (!event.shiftKey && (active === items.at(-1) || (!container.current?.contains(active) && !belongsToOverlay(active, id)))) { event.preventDefault(); items[0].focus(); }
    };
    document.addEventListener('keydown', trap, true);
    return () => { cancelAnimationFrame(frame); document.removeEventListener('keydown', trap, true); if (previous && document.contains(previous)) previous.focus(); };
  }, [id, open, container, initial]);
}

export function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => window.matchMedia(query).matches);
  useEffect(() => { const media = window.matchMedia(query); const update = () => setMatches(media.matches); update(); media.addEventListener('change', update); return () => media.removeEventListener('change', update); }, [query]);
  return matches;
}

export function useFloatingPanel(open: boolean, anchor: RefObject<HTMLElement | null>, panel: RefObject<HTMLElement | null>, preferredWidth: number) {
  const [position, setPosition] = useState({ left: 8, top: 8, width: preferredWidth, maxHeight: 320 });
  useLayoutEffect(() => {
    if (!open) return;
    const place = () => {
      const rect = anchor.current?.getBoundingClientRect(); if (!rect) return;
      const viewport = window.visualViewport;
      const viewportWidth = viewport?.width ?? window.innerWidth; const viewportHeight = viewport?.height ?? window.innerHeight;
      const offsetLeft = viewport?.offsetLeft ?? 0; const offsetTop = viewport?.offsetTop ?? 0;
      const width = Math.min(preferredWidth, viewportWidth - 16);
      const left = Math.max(offsetLeft + 8, Math.min(getComputedStyle(anchor.current!).direction === 'rtl' ? rect.right - width : rect.left, offsetLeft + viewportWidth - width - 8));
      const height = Math.min(panel.current?.scrollHeight ?? 0, viewportHeight - 16);
      const below = rect.bottom + 8;
      const top = Math.max(offsetTop + 8, Math.min(below + height <= offsetTop + viewportHeight - 8 ? below : rect.top - height - 8, offsetTop + viewportHeight - height - 8));
      setPosition(old => old.left === left && old.top === top && old.width === width && old.maxHeight === viewportHeight - 16 ? old : { left, top, width, maxHeight: viewportHeight - 16 });
    };
    place(); const observer = new ResizeObserver(place); if (panel.current) observer.observe(panel.current);
    window.addEventListener('resize', place); window.addEventListener('scroll', place, true);
    window.visualViewport?.addEventListener('resize', place); window.visualViewport?.addEventListener('scroll', place);
    return () => { observer.disconnect(); window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true); window.visualViewport?.removeEventListener('resize', place); window.visualViewport?.removeEventListener('scroll', place); };
  }, [open, anchor, panel, preferredWidth]);
  return position;
}
