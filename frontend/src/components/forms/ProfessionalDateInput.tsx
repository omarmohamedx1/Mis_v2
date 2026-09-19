import { CalendarDays, ChevronLeft, ChevronRight, Clock3, X } from 'lucide-react';
import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useLocalization } from '../../context/LocalizationContext';

interface ProfessionalDateInputProps {
  id?: string;
  name?: string;
  value?: string;
  defaultValue?: string;
  onChange?: (value: string) => void;
  mode?: 'date' | 'datetime';
  required?: boolean;
  min?: string;
  max?: string;
  disabled?: boolean;
  className?: string;
  ariaLabel?: string;
}

const pad = (value: number) => String(value).padStart(2, '0');
const isoDate = (date: Date) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;

function westernDigits(value: string) {
  return value
    .replace(/[٠-٩]/g, (digit) => String(digit.charCodeAt(0) - 1632))
    .replace(/[۰-۹]/g, (digit) => String(digit.charCodeAt(0) - 1776));
}

function buildDate(year: number, month: number, day: number, hour = 0, minute = 0) {
  const fullYear = year < 100 ? (year >= 50 ? 1900 + year : 2000 + year) : year;
  const date = new Date(fullYear, month - 1, day, hour, minute);
  if (date.getFullYear() !== fullYear || date.getMonth() !== month - 1 || date.getDate() !== day) return null;
  return date;
}

function parseValue(value?: string) {
  if (!value) return null;
  const raw = westernDigits(value).trim().replace(/\s+/g, ' ');
  if (!raw) return null;

  const iso = raw.match(/^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{1,2}):(\d{2}))?$/);
  if (iso) return buildDate(Number(iso[1]), Number(iso[2]), Number(iso[3]), Number(iso[4] ?? 0), Number(iso[5] ?? 0));

  const dmy = raw.match(/^(\d{1,2})[/.\-](\d{1,2})[/.\-](\d{2,4})(?:[ T](\d{1,2}):(\d{2}))?$/);
  if (dmy) return buildDate(Number(dmy[3]), Number(dmy[2]), Number(dmy[1]), Number(dmy[4] ?? 0), Number(dmy[5] ?? 0));

  const digits = raw.replace(/\D/g, '');
  if (digits.length === 8) {
    const asIso = buildDate(Number(digits.slice(0, 4)), Number(digits.slice(4, 6)), Number(digits.slice(6, 8)));
    if (Number(digits.slice(0, 4)) >= 1900 && asIso) return asIso;
    return buildDate(Number(digits.slice(4)), Number(digits.slice(2, 4)), Number(digits.slice(0, 2)));
  }
  if (digits.length === 12) {
    return buildDate(Number(digits.slice(4, 8)), Number(digits.slice(2, 4)), Number(digits.slice(0, 2)), Number(digits.slice(8, 10)), Number(digits.slice(10, 12)));
  }
  return null;
}

function formatTyped(date: Date | null, mode: 'date' | 'datetime') {
  if (!date) return '';
  const text = `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}`;
  return mode === 'datetime' ? `${text} ${pad(date.getHours())}:${pad(date.getMinutes())}` : text;
}

function hasTime(value: string) {
  return /[T ]\d{1,2}:\d{2}/.test(westernDigits(value).trim());
}

export function ProfessionalDateInput({
  id, name, value, defaultValue, onChange, mode = 'date', required, min, max, disabled, className = '', ariaLabel,
}: ProfessionalDateInputProps) {
  const { language, isRtl } = useLocalization();
  const root = useRef<HTMLDivElement>(null);
  const popup = useRef<HTMLDivElement>(null);
  const typing = useRef(false);
  const controlled = value !== undefined;
  const [internal, setInternal] = useState(defaultValue ?? '');
  const current = controlled ? value ?? '' : internal;
  const selected = parseValue(current);
  const [open, setOpen] = useState(false);
  const [view, setView] = useState(() => selected ?? new Date());
  const [hour, setHour] = useState(() => pad(selected?.getHours() ?? 9));
  const [minute, setMinute] = useState(() => pad(selected?.getMinutes() ?? 0));
  const [draft, setDraft] = useState(() => formatTyped(selected, mode));
  const [yearDraft, setYearDraft] = useState(() => String((selected ?? new Date()).getFullYear()));
  const [position, setPosition] = useState({ left: 8, top: 8, width: 360 });

  useEffect(() => {
    const next = parseValue(current);
    if (!typing.current) setDraft(formatTyped(next, mode));
    if (next) {
      setView(next);
      setHour(pad(next.getHours()));
      setMinute(pad(next.getMinutes()));
      setYearDraft(String(next.getFullYear()));
    }
  }, [current, mode]);

  useEffect(() => {
    const close = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!root.current?.contains(target) && !popup.current?.contains(target)) setOpen(false);
    };
    document.addEventListener('pointerdown', close);
    return () => document.removeEventListener('pointerdown', close);
  }, []);

  useEffect(() => {
    if (!open) return undefined;
    const place = () => {
      const anchor = root.current?.getBoundingClientRect();
      if (!anchor) return;
      const width = Math.min(360, window.innerWidth - 16);
      const preferredLeft = isRtl ? anchor.right - width : anchor.left;
      const left = Math.max(8, Math.min(preferredLeft, window.innerWidth - width - 8));
      const estimatedHeight = mode === 'datetime' ? 500 : 440;
      const below = anchor.bottom + 8;
      const top = below + estimatedHeight <= window.innerHeight ? below : Math.max(8, anchor.top - estimatedHeight - 8);
      setPosition({ left, top, width });
    };
    place();
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false); };
    document.addEventListener('keydown', escape);
    return () => {
      window.removeEventListener('resize', place);
      window.removeEventListener('scroll', place, true);
      document.removeEventListener('keydown', escape);
    };
  }, [isRtl, mode, open]);

  useEffect(() => {
    const form = root.current?.closest('form');
    if (!form || controlled) return undefined;
    const reset = () => {
      setInternal(defaultValue ?? '');
      setDraft(formatTyped(parseValue(defaultValue), mode));
      setOpen(false);
    };
    form.addEventListener('reset', reset);
    return () => form.removeEventListener('reset', reset);
  }, [controlled, defaultValue, mode]);

  const locale = language === 'ar' ? 'ar-EG' : 'en-GB';
  const labels = language === 'ar'
    ? { choose: mode === 'date' ? 'اختر أو اكتب التاريخ' : 'اختر أو اكتب التاريخ والوقت', today: 'اليوم', clear: 'مسح', done: 'تم', previous: 'الشهر السابق', next: 'الشهر التالي', time: 'الوقت', month: 'الشهر', year: 'السنة', open: 'فتح التقويم' }
    : { choose: mode === 'date' ? 'Type or choose a date' : 'Type or choose date & time', today: 'Today', clear: 'Clear', done: 'Done', previous: 'Previous month', next: 'Next month', time: 'Time', month: 'Month', year: 'Year', open: 'Open calendar' };
  const placeholder = mode === 'datetime' ? '31/12/2026 09:00' : '31/12/2026';
  const minimum = min?.slice(0, 10) ?? '';
  const maximum = max?.slice(0, 10) ?? '';
  const minYear = minimum ? Number(minimum.slice(0, 4)) : 1920;
  const maxYear = maximum ? Number(maximum.slice(0, 4)) : new Date().getFullYear() + 20;
  const months = useMemo(() => Array.from({ length: 12 }, (_, index) => new Intl.DateTimeFormat(locale, { month: 'long' }).format(new Date(2024, index, 1))), [locale]);
  const years = useMemo(() => Array.from({ length: maxYear - minYear + 1 }, (_, index) => minYear + index), [maxYear, minYear]);
  const yearListId = useId();
  const minutes = useMemo(() => {
    const options = Array.from({ length: 12 }, (_, index) => pad(index * 5));
    return options.includes(minute) ? options : [...options, minute].sort();
  }, [minute]);

  const emit = (date: Date | null, close = mode === 'date') => {
    if (date && mode === 'datetime') {
      setHour(pad(date.getHours()));
      setMinute(pad(date.getMinutes()));
    }
    const next = date ? (mode === 'datetime' ? `${isoDate(date)}T${pad(date.getHours())}:${pad(date.getMinutes())}` : isoDate(date)) : '';
    if (!controlled) setInternal(next);
    onChange?.(next);
    typing.current = false;
    setDraft(formatTyped(date, mode));
    if (close) setOpen(false);
  };

  const inRange = (date: Date) => {
    const key = isoDate(date);
    return !((minimum && key < minimum) || (maximum && key > maximum));
  };

  const applyTyped = (text: string, commitEmpty = false) => {
    const trimmed = text.trim();
    if (!trimmed) {
      if (commitEmpty) emit(null, false);
      return false;
    }
    const parsed = parseValue(trimmed);
    if (!parsed || !inRange(parsed)) return false;
    if (mode === 'datetime' && !hasTime(trimmed)) {
      parsed.setHours(Number(hour), Number(minute));
    } else if (mode === 'datetime') {
      setHour(pad(parsed.getHours()));
      setMinute(pad(parsed.getMinutes()));
    }
    setView(parsed);
    setYearDraft(String(parsed.getFullYear()));
    emit(parsed, false);
    return true;
  };

  const monthStart = new Date(view.getFullYear(), view.getMonth(), 1);
  const gridStart = new Date(monthStart);
  gridStart.setDate(1 - ((monthStart.getDay() + 6) % 7));
  const days = useMemo(() => Array.from({ length: 42 }, (_, index) => {
    const date = new Date(gridStart);
    date.setDate(gridStart.getDate() + index);
    return date;
  }), [gridStart.getTime()]);
  const weekdays = useMemo(() => Array.from({ length: 7 }, (_, index) => new Intl.DateTimeFormat(locale, { weekday: 'narrow' }).format(new Date(2024, 0, index + 1))), [locale]);
  const choose = (date: Date) => {
    if (!inRange(date)) return;
    const next = new Date(date.getFullYear(), date.getMonth(), date.getDate(), Number(hour), Number(minute));
    setView(next);
    setYearDraft(String(next.getFullYear()));
    emit(next, mode === 'date');
  };
  const applyTime = () => {
    const basis = selected ?? view;
    emit(new Date(basis.getFullYear(), basis.getMonth(), basis.getDate(), Number(hour), Number(minute)), true);
  };
  const shiftMonth = (offset: number) => {
    const next = new Date(view.getFullYear(), view.getMonth() + offset, 1);
    setView(next);
    setYearDraft(String(next.getFullYear()));
  };
  const jumpYear = (year: number) => {
    if (year < minYear || year > maxYear) return;
    setView(new Date(year, view.getMonth(), 1));
    setYearDraft(String(year));
  };

  const calendar = open ? createPortal(
    <div ref={popup} className="fixed z-[100] max-h-[calc(100vh-1rem)] overflow-y-auto rounded-2xl border border-mis-border bg-white p-4 shadow-2xl" dir={isRtl ? 'rtl' : 'ltr'} style={{ left: position.left, top: position.top, width: position.width }}>
      <div className="flex items-center gap-2">
        <button aria-label={labels.previous} className="grid h-10 w-10 shrink-0 place-items-center rounded-xl text-slate-500 hover:bg-mis-pale hover:text-mis-primary" onClick={() => shiftMonth(-1)} type="button">
          <ChevronLeft className={`h-5 w-5 ${isRtl ? 'rotate-180' : ''}`} />
        </button>
        <select aria-label={labels.month} className="h-10 min-w-0 flex-1 rounded-xl border border-mis-border bg-white px-2 text-sm font-semibold text-mis-navy outline-none focus:border-mis-blue" onChange={(event) => setView(new Date(view.getFullYear(), Number(event.target.value), 1))} value={view.getMonth()}>
          {months.map((label, index) => <option key={label} value={index}>{label}</option>)}
        </select>
        <input
          aria-label={labels.year}
          className="h-10 w-[4.85rem] rounded-xl border border-mis-border bg-white px-2 text-center text-sm font-bold tabular-nums text-mis-navy outline-none focus:border-mis-blue"
          dir="ltr"
          inputMode="numeric"
          list={yearListId}
          maxLength={4}
          onBlur={() => setYearDraft(String(view.getFullYear()))}
          onChange={(event) => {
            const next = event.target.value.replace(/\D/g, '').slice(0, 4);
            setYearDraft(next);
            if (next.length === 4) jumpYear(Number(next));
          }}
          value={yearDraft}
        />
        <datalist id={yearListId}>{years.map((year) => <option key={year} value={year} />)}</datalist>
        <button aria-label={labels.next} className="grid h-10 w-10 shrink-0 place-items-center rounded-xl text-slate-500 hover:bg-mis-pale hover:text-mis-primary" onClick={() => shiftMonth(1)} type="button">
          <ChevronRight className={`h-5 w-5 ${isRtl ? 'rotate-180' : ''}`} />
        </button>
      </div>
      <div className="mt-3 grid grid-cols-7 text-center text-xs font-bold text-slate-400">{weekdays.map((day, index) => <span className="py-2" key={`${day}-${index}`}>{day}</span>)}</div>
      <div className="grid grid-cols-7 gap-1">
        {days.map((day) => {
          const key = isoDate(day);
          const active = selected && key === isoDate(selected);
          const today = key === isoDate(new Date());
          const outside = day.getMonth() !== view.getMonth();
          return (
            <button
              className={`grid h-9 place-items-center rounded-lg text-sm transition disabled:cursor-not-allowed disabled:opacity-25 ${active ? 'bg-mis-primary font-bold text-white shadow-sm' : today ? 'bg-mis-pale font-bold text-mis-primary' : outside ? 'text-slate-300 hover:bg-slate-50' : 'text-slate-700 hover:bg-mis-pale hover:text-mis-primary'}`}
              disabled={!inRange(day)}
              key={key}
              onClick={() => choose(day)}
              type="button"
            >
              {new Intl.NumberFormat(locale, { useGrouping: false }).format(day.getDate())}
            </button>
          );
        })}
      </div>
      {mode === 'datetime' ? (
        <div className="mt-4 flex items-center gap-3 rounded-xl bg-slate-50 p-3">
          <Clock3 className="h-5 w-5 text-mis-primary" />
          <span className="text-xs font-bold text-slate-500">{labels.time}</span>
          <div className="ms-auto flex items-center gap-1" dir="ltr">
            <select aria-label={language === 'ar' ? 'الساعة' : 'Hour'} className="h-9 w-[4.5rem] rounded-lg border border-mis-border bg-white px-2 text-sm outline-none focus:border-mis-blue" onChange={(event) => setHour(event.target.value)} value={hour}>
              {Array.from({ length: 24 }, (_, index) => pad(index)).map((item) => <option key={item}>{item}</option>)}
            </select>
            <span>:</span>
            <select aria-label={language === 'ar' ? 'الدقيقة' : 'Minute'} className="h-9 w-[4.5rem] rounded-lg border border-mis-border bg-white px-2 text-sm outline-none focus:border-mis-blue" onChange={(event) => setMinute(event.target.value)} value={minute}>
              {minutes.map((item) => <option key={item}>{item}</option>)}
            </select>
          </div>
        </div>
      ) : null}
      <div className="mt-4 flex items-center gap-2 border-t border-mis-border pt-3">
        <button className="min-h-9 rounded-lg px-3 py-2 text-xs font-bold text-mis-primary hover:bg-mis-pale" onClick={() => { const now = new Date(); setView(now); setYearDraft(String(now.getFullYear())); setHour(pad(now.getHours())); setMinute(pad(Math.floor(now.getMinutes() / 5) * 5)); emit(now, mode === 'date'); }} type="button">{labels.today}</button>
        <button className="min-h-9 rounded-lg px-3 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100" onClick={() => emit(null)} type="button">{labels.clear}</button>
        {mode === 'datetime' ? <button className="ms-auto min-h-9 rounded-lg bg-mis-primary px-4 py-2 text-xs font-bold text-white" onClick={applyTime} type="button">{labels.done}</button> : null}
      </div>
    </div>,
    document.body,
  ) : null;

  return (
    <div className={`relative min-w-0 ${className}`} ref={root}>
      <input aria-label={ariaLabel ?? labels.choose} className="pointer-events-none absolute h-px w-px opacity-0" name={name} onInvalid={() => setOpen(true)} readOnly required={required} tabIndex={-1} type="text" value={current} />
      <div className={`field flex min-h-11 items-center gap-1 p-1 ps-2 ${disabled ? 'bg-slate-100 opacity-70' : ''}`}>
        <button aria-expanded={open} aria-label={labels.open} className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-mis-primary hover:bg-mis-pale disabled:opacity-50" disabled={disabled} onClick={() => setOpen((currentOpen) => !currentOpen)} type="button">
          <CalendarDays className="h-5 w-5" />
        </button>
        <input
          aria-label={ariaLabel ?? labels.choose}
          autoComplete="off"
          className="min-h-9 min-w-0 flex-1 bg-transparent px-1 text-sm text-slate-800 outline-none placeholder:text-slate-400"
          dir="ltr"
          disabled={disabled}
          id={id}
          onBlur={() => {
            typing.current = false;
            if (!applyTyped(draft, true)) setDraft(formatTyped(parseValue(current), mode));
          }}
          onChange={(event) => {
            typing.current = true;
            const next = event.target.value;
            setDraft(next);
            applyTyped(next);
          }}
          onFocus={() => { typing.current = true; }}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              typing.current = false;
              if (applyTyped(draft, true)) (event.currentTarget as HTMLInputElement).blur();
            } else if (event.key === 'ArrowDown') {
              event.preventDefault();
              setOpen(true);
            }
          }}
          placeholder={placeholder}
          spellCheck={false}
          value={draft}
        />
        {selected && !disabled ? (
          <button aria-label={labels.clear} className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-rose-600" onClick={() => emit(null)} type="button">
            <X className="h-4 w-4" />
          </button>
        ) : null}
      </div>
      {calendar}
    </div>
  );
}
