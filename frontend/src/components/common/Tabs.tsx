import { useId, type HTMLAttributes, type KeyboardEvent, type ReactNode } from 'react';

export interface TabItem {
  badge?: ReactNode;
  disabled?: boolean;
  icon?: ReactNode;
  id: string;
  label: ReactNode;
  panelId?: string;
}

export interface TabsProps {
  activationMode?: 'automatic' | 'manual';
  ariaLabel: string;
  className?: string;
  items: TabItem[];
  onChange: (id: string) => void;
  orientation?: 'horizontal' | 'vertical';
  wrap?: boolean;
  value: string;
}

export function Tabs({ activationMode = 'automatic', ariaLabel, className = '', items, onChange, orientation = 'horizontal', value, wrap = false }: TabsProps) {
  const generatedId = useId();

  function moveFocus(event: KeyboardEvent<HTMLButtonElement>, itemId: string) {
    const enabledItems = items.filter((item) => !item.disabled);
    if (enabledItems.length === 0) return;

    const currentIndex = enabledItems.findIndex((item) => item.id === itemId);
    const rtlMultiplier = document.documentElement.dir === 'rtl' ? -1 : 1;
    let targetIndex: number | null = null;

    if (event.key === 'Home') targetIndex = 0;
    if (event.key === 'End') targetIndex = enabledItems.length - 1;
    if (orientation === 'horizontal' && event.key === 'ArrowRight') targetIndex = (currentIndex + rtlMultiplier + enabledItems.length) % enabledItems.length;
    if (orientation === 'horizontal' && event.key === 'ArrowLeft') targetIndex = (currentIndex - rtlMultiplier + enabledItems.length) % enabledItems.length;
    if (orientation === 'vertical' && event.key === 'ArrowDown') targetIndex = (currentIndex + 1) % enabledItems.length;
    if (orientation === 'vertical' && event.key === 'ArrowUp') targetIndex = (currentIndex - 1 + enabledItems.length) % enabledItems.length;

    if (targetIndex === null) return;
    event.preventDefault();
    const target = enabledItems[targetIndex];
    document.getElementById(`${generatedId}-${target.id}`)?.focus();
    if (activationMode === 'automatic') onChange(target.id);
  }

  return (
    <div
      aria-label={ariaLabel}
      aria-orientation={orientation}
      className={`${orientation === 'horizontal' ? wrap ? 'grid grid-cols-2 gap-2 p-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6' : 'flex min-w-0 overflow-x-auto p-1 border-b border-mis-border' : 'flex flex-col border-e border-mis-border'} ${className}`}
      role="tablist"
    >
      {items.map((item) => {
        const selected = item.id === value;
        return (
          <button
            aria-controls={item.panelId}
            aria-selected={selected}
            className={`inline-flex min-h-11 min-w-0 max-w-full flex-none items-center justify-center gap-2 px-4 text-sm font-semibold transition focus-visible:relative disabled:cursor-not-allowed disabled:opacity-40 ${orientation === 'horizontal' ? wrap ? `rounded-xl border ${selected ? 'border-mis-primary bg-mis-primary text-white shadow-sm' : 'border-slate-200 bg-slate-50 text-slate-700 hover:border-mis-sky hover:bg-white hover:text-mis-deep'}` : `border-b-2 border-mis-primary ${selected ? 'border-mis-primary text-mis-deep' : 'border-transparent text-slate-600 hover:border-mis-sky hover:text-mis-primary'}` : `border-e-2 border-mis-primary ${selected ? 'border-mis-primary bg-mis-pale/50 text-mis-deep' : 'border-transparent text-slate-600 hover:bg-slate-50 hover:text-mis-primary'}`}`}
            disabled={item.disabled}
            id={`${generatedId}-${item.id}`}
            key={item.id}
            onClick={() => onChange(item.id)}
            onKeyDown={(event) => moveFocus(event, item.id)}
            role="tab"
            tabIndex={selected ? 0 : -1}
            type="button"
          >
            {item.icon ? <span className="shrink-0">{item.icon}</span> : null}
            <span className="min-w-0 [overflow-wrap:anywhere]">{item.label}</span>
            {item.badge !== undefined ? <span className="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{item.badge}</span> : null}
          </button>
        );
      })}
    </div>
  );
}

export interface TabPanelProps extends HTMLAttributes<HTMLDivElement> {
  active: boolean;
  labelledBy?: string;
}

export function TabPanel({ active, children, className = '', labelledBy, ...props }: TabPanelProps) {
  if (!active) return null;
  return (
    <div aria-labelledby={labelledBy} className={className} role="tabpanel" tabIndex={0} {...props}>
      {children}
    </div>
  );
}
