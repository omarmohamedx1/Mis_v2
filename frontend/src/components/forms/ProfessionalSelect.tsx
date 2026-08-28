import { Check, ChevronDown, Search, X } from 'lucide-react';
import {
  Children,
  cloneElement,
  forwardRef,
  isValidElement,
  useEffect,
  useId,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type KeyboardEvent,
  type OptionHTMLAttributes,
  type ReactElement,
  type ReactNode,
  type SelectHTMLAttributes,
} from 'react';
import { createPortal } from 'react-dom';
import { useLocalization } from '../../context/LocalizationContext';

interface SelectOption {
  disabled: boolean;
  key: string;
  label: ReactNode;
  searchText: string;
  value: string;
}

export interface ProfessionalSelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'multiple' | 'size'> {
  children: ReactNode;
  emptyMessage?: string;
  searchPlaceholder?: string;
}

function nodeText(node: ReactNode): string {
  if (typeof node === 'string' || typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(nodeText).join(' ');
  if (isValidElement<{ children?: ReactNode }>(node)) return nodeText(node.props.children);
  return '';
}

function readOptions(children: ReactNode): SelectOption[] {
  const result: SelectOption[] = [];
  const visit = (nodes: ReactNode) => {
    Children.forEach(nodes, (node) => {
      if (!isValidElement(node)) return;
      if (node.type === 'option') {
        const option = node as ReactElement<OptionHTMLAttributes<HTMLOptionElement>>;
        const label = option.props.children;
        const value = String(option.props.value ?? nodeText(label));
        result.push({
          disabled: Boolean(option.props.disabled),
          key: String(option.key ?? value),
          label,
          searchText: nodeText(label).toLocaleLowerCase(),
          value,
        });
        return;
      }
      if (node.type === 'optgroup') visit((node.props as { children?: ReactNode }).children);
    });
  };
  visit(children);
  return result;
}

function scalarValue(value: SelectHTMLAttributes<HTMLSelectElement>['value'] | SelectHTMLAttributes<HTMLSelectElement>['defaultValue']): string | undefined {
  if (Array.isArray(value)) return value[0] === undefined ? undefined : String(value[0]);
  return value === undefined || value === null ? undefined : String(value);
}

export const ProfessionalSelect = forwardRef<HTMLSelectElement, ProfessionalSelectProps>(function ProfessionalSelect(
  {
    'aria-describedby': ariaDescribedBy,
    'aria-invalid': ariaInvalid,
    'aria-label': ariaLabel,
    autoFocus,
    children,
    className = '',
    defaultValue,
    disabled,
    emptyMessage,
    id,
    name,
    onBlur,
    onChange,
    onFocus,
    onInvalid,
    required,
    searchPlaceholder,
    tabIndex,
    title,
    value,
    ...nativeProps
  },
  forwardedRef,
) {
  const { isRtl, language } = useLocalization();
  const generatedId = useId();
  const selectId = id ?? `professional-select-${generatedId.replaceAll(':', '')}`;
  const listboxId = `${selectId}-listbox`;
  const triggerRef = useRef<HTMLButtonElement>(null);
  const nativeRef = useRef<HTMLSelectElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const options = useMemo(() => readOptions(children), [children]);
  const controlledValue = scalarValue(value);
  const initialValue = scalarValue(defaultValue) ?? options.find((option) => !option.disabled)?.value ?? '';
  const [internalValue, setInternalValue] = useState(initialValue);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);
  const [layout, setLayout] = useState({ bottom: undefined as number | undefined, left: 12, maxHeight: 320, mobile: false, top: 0, width: 288 });
  const selectedValue = controlledValue ?? internalValue;
  const selectedOption = options.find((option) => option.value === selectedValue) ?? options[0];
  const normalizedQuery = query.trim().toLocaleLowerCase();
  const visibleOptions = normalizedQuery ? options.filter((option) => option.searchText.includes(normalizedQuery)) : options;
  const showSearch = options.length >= 7;

  useImperativeHandle(forwardedRef, () => nativeRef.current as HTMLSelectElement, []);

  useEffect(() => {
    if (controlledValue === undefined && !options.some((option) => option.value === internalValue)) {
      setInternalValue(scalarValue(defaultValue) ?? options.find((option) => !option.disabled)?.value ?? '');
    }
  }, [controlledValue, defaultValue, internalValue, options]);

  useEffect(() => {
    const select = nativeRef.current;
    const form = select?.form;
    if (!form) return undefined;
    const reset = () => setInternalValue(scalarValue(defaultValue) ?? options.find((option) => !option.disabled)?.value ?? '');
    form.addEventListener('reset', reset);
    return () => form.removeEventListener('reset', reset);
  }, [defaultValue, options]);

  useEffect(() => {
    if (!open) return undefined;
    const updateLayout = () => {
      const trigger = triggerRef.current;
      if (!trigger) return;
      const rect = trigger.getBoundingClientRect();
      const mobile = window.innerWidth < 640;
      if (mobile) {
        setLayout({ bottom: 12, left: 12, maxHeight: Math.min(560, window.innerHeight - 24), mobile: true, top: 0, width: window.innerWidth - 24 });
        return;
      }
      const width = Math.min(Math.max(rect.width, 260), window.innerWidth - 24);
      const left = Math.min(Math.max(isRtl ? rect.right - width : rect.left, 12), window.innerWidth - width - 12);
      const below = window.innerHeight - rect.bottom - 12;
      const above = rect.top - 12;
      const openAbove = below < 240 && above > below;
      setLayout({
        bottom: openAbove ? window.innerHeight - rect.top + 6 : undefined,
        left,
        maxHeight: Math.max(160, Math.min(360, openAbove ? above - 6 : below - 6)),
        mobile: false,
        top: openAbove ? 0 : rect.bottom + 6,
        width,
      });
    };
    updateLayout();
    window.addEventListener('resize', updateLayout);
    window.addEventListener('scroll', updateLayout, true);
    return () => {
      window.removeEventListener('resize', updateLayout);
      window.removeEventListener('scroll', updateLayout, true);
    };
  }, [isRtl, open]);

  useEffect(() => {
    if (!open) return;
    setQuery('');
    const selectedIndex = Math.max(0, options.findIndex((option) => option.value === selectedValue));
    setActiveIndex(selectedIndex);
    window.setTimeout(() => {
      if (showSearch) searchRef.current?.focus();
      else optionRefs.current[selectedIndex]?.focus();
    }, 0);
  }, [open, options, selectedValue, showSearch]);

  const close = (restoreFocus = true) => {
    setOpen(false);
    setQuery('');
    if (restoreFocus) window.setTimeout(() => triggerRef.current?.focus(), 0);
  };

  const commit = (nextValue: string) => {
    const option = options.find((item) => item.value === nextValue);
    if (!option || option.disabled) return;
    if (controlledValue === undefined) setInternalValue(nextValue);
    if (nativeRef.current) {
      nativeRef.current.value = nextValue;
      onChange?.({ target: nativeRef.current, currentTarget: nativeRef.current } as ChangeEvent<HTMLSelectElement>);
    }
    close();
  };

  const moveActive = (direction: 1 | -1) => {
    if (!visibleOptions.length) return;
    setActiveIndex((current) => {
      let next = current;
      for (let attempt = 0; attempt < visibleOptions.length; attempt += 1) {
        next = (next + direction + visibleOptions.length) % visibleOptions.length;
        if (!visibleOptions[next]?.disabled) break;
      }
      window.setTimeout(() => optionRefs.current[next]?.focus(), 0);
      return next;
    });
  };

  const handleTriggerKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) {
      event.preventDefault();
      setOpen(true);
    }
  };

  const handleOptionKeyDown = (event: KeyboardEvent<HTMLButtonElement>, option: SelectOption) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      moveActive(event.key === 'ArrowDown' ? 1 : -1);
    } else if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      const index = event.key === 'Home' ? 0 : visibleOptions.length - 1;
      setActiveIndex(index);
      optionRefs.current[index]?.focus();
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      commit(option.value);
    } else if (event.key === 'Escape' || event.key === 'Tab') {
      close(event.key !== 'Tab');
    }
  };

  const menu = open && typeof document !== 'undefined' ? createPortal(
    <>
      <button
        aria-label={language === 'ar' ? 'إغلاق قائمة الاختيارات' : 'Close options'}
        className={`fixed inset-0 z-[100] cursor-default ${layout.mobile ? 'bg-slate-950/45 backdrop-blur-[1px]' : 'bg-transparent'}`}
        onClick={() => close()}
        tabIndex={-1}
        type="button"
      />
      <section
        aria-label={ariaLabel ?? title ?? (language === 'ar' ? 'الاختيارات المتاحة' : 'Available options')}
        className={`fixed z-[110] flex overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-[0_24px_70px_rgba(15,23,42,.24)] ${layout.mobile ? 'max-h-[calc(100dvh-1.5rem)] flex-col' : 'flex-col'}`}
        dir={isRtl ? 'rtl' : 'ltr'}
        style={{ bottom: layout.bottom, left: layout.left, maxHeight: layout.maxHeight, top: layout.bottom === undefined ? layout.top : undefined, width: layout.width }}
      >
        <div className="flex shrink-0 items-center gap-3 border-b border-slate-200 bg-slate-50/80 px-4 py-3">
          <div className="min-w-0 flex-1">
            <p className="truncate text-xs font-black uppercase tracking-wider text-slate-500">{language === 'ar' ? 'اختر من القائمة' : 'Select an option'}</p>
            <p className="mt-0.5 truncate text-sm font-bold text-mis-navy">{selectedOption?.label ?? (language === 'ar' ? 'لم يتم الاختيار' : 'Nothing selected')}</p>
          </div>
          <button aria-label={language === 'ar' ? 'إغلاق' : 'Close'} className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-slate-500 hover:bg-white hover:text-mis-navy" onClick={() => close()} type="button"><X className="h-4 w-4" /></button>
        </div>
        {showSearch ? <label className="relative m-3 mb-1 shrink-0"><Search aria-hidden="true" className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" /><span className="sr-only">{searchPlaceholder ?? (language === 'ar' ? 'البحث في الاختيارات' : 'Search options')}</span><input ref={searchRef} className="h-11 w-full rounded-xl border border-slate-200 bg-white ps-10 pe-3 text-sm text-slate-800 outline-none focus:border-mis-blue focus:ring-4 focus:ring-mis-pale" onChange={(event) => { setQuery(event.target.value); setActiveIndex(0); }} onKeyDown={(event) => { if (event.key === 'Escape') close(); else if (event.key === 'ArrowDown') { event.preventDefault(); optionRefs.current[0]?.focus(); } }} placeholder={searchPlaceholder ?? (language === 'ar' ? 'ابحث...' : 'Search...')} value={query} /></label> : null}
        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain p-2" id={listboxId} role="listbox">
          {visibleOptions.length ? visibleOptions.map((option, index) => {
            const selected = option.value === selectedValue;
            return <button
              aria-selected={selected}
              className={`flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2.5 text-start text-sm transition ${selected ? 'bg-mis-pale font-bold text-mis-primary' : 'text-slate-700 hover:bg-slate-50 hover:text-mis-navy'} disabled:cursor-not-allowed disabled:opacity-45`}
              disabled={option.disabled}
              id={`${listboxId}-option-${index}`}
              key={option.key}
              onClick={() => commit(option.value)}
              onFocus={() => setActiveIndex(index)}
              onKeyDown={(event) => handleOptionKeyDown(event, option)}
              ref={(element) => { optionRefs.current[index] = element; }}
              role="option"
              tabIndex={index === activeIndex ? 0 : -1}
              type="button"
            >
              <span className="min-w-0 flex-1 leading-6">{option.label}</span>
              {selected ? <span className="inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-mis-primary text-white"><Check className="h-3.5 w-3.5" /></span> : null}
            </button>;
          }) : <div className="px-4 py-10 text-center text-sm text-slate-500">{emptyMessage ?? (language === 'ar' ? 'لا توجد نتائج مطابقة' : 'No matching options')}</div>}
        </div>
      </section>
    </>,
    document.body,
  ) : null;

  return (
    <div className="relative min-w-0">
      <select
        {...nativeProps}
        aria-hidden="true"
        className="sr-only"
        defaultValue={undefined}
        disabled={disabled}
        name={name}
        onChange={(event) => {
          if (controlledValue === undefined) setInternalValue(event.target.value);
          onChange?.(event);
        }}
        onInvalid={(event) => {
          onInvalid?.(event);
          triggerRef.current?.focus();
          setOpen(true);
        }}
        ref={nativeRef}
        required={required}
        tabIndex={-1}
        value={selectedValue}
      >
        {Children.map(children, (child) => isValidElement(child) ? cloneElement(child) : child)}
      </select>
      <button
        aria-controls={open ? listboxId : undefined}
        aria-describedby={ariaDescribedBy}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-invalid={ariaInvalid}
        aria-label={ariaLabel}
        autoFocus={autoFocus}
        className={`flex min-h-11 w-full items-center justify-between gap-3 rounded-xl border border-mis-border bg-white px-4 py-2.5 text-start text-sm text-slate-800 outline-none transition hover:border-slate-300 focus:border-mis-blue focus:ring-4 focus:ring-mis-pale disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500 ${className}`}
        disabled={disabled}
        id={selectId}
        onBlur={(event) => onBlur?.({ target: nativeRef.current, currentTarget: nativeRef.current } as ChangeEvent<HTMLSelectElement> & typeof event)}
        onClick={() => setOpen((current) => !current)}
        onFocus={(event) => onFocus?.({ target: nativeRef.current, currentTarget: nativeRef.current } as ChangeEvent<HTMLSelectElement> & typeof event)}
        onKeyDown={handleTriggerKeyDown}
        ref={triggerRef}
        tabIndex={tabIndex}
        title={title}
        type="button"
      >
        <span className={`min-w-0 flex-1 truncate ${selectedValue === '' ? 'text-slate-500' : ''}`}>{selectedOption?.label ?? '—'}</span>
        <ChevronDown aria-hidden="true" className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>
      {menu}
    </div>
  );
});
