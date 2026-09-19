import { Clock3, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useLocalization } from '../../context/LocalizationContext';
import { ProfessionalSelect } from './ProfessionalSelect';

interface ProfessionalTimeInputProps {
  ariaLabel?: string;
  className?: string;
  defaultValue?: string;
  disabled?: boolean;
  id?: string;
  name?: string;
  onChange?: (value: string) => void;
  required?: boolean;
  value?: string;
}

const pad = (value: number) => String(value).padStart(2, '0');
const hours = Array.from({ length: 24 }, (_, index) => pad(index));
const minutes = Array.from({ length: 60 }, (_, index) => pad(index));

function parseTime(value?: string): { hour: string; minute: string } | null {
  const match = value?.trim().match(/^(\d{1,2}):(\d{2})/);
  if (!match) return null;
  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (hour > 23 || minute > 59) return null;
  return { hour: pad(hour), minute: pad(minute) };
}

export function ProfessionalTimeInput({
  ariaLabel,
  className = '',
  defaultValue,
  disabled = false,
  id,
  name,
  onChange,
  required,
  value,
}: ProfessionalTimeInputProps) {
  const { language, isRtl } = useLocalization();
  const root = useRef<HTMLDivElement>(null);
  const popup = useRef<HTMLDivElement>(null);
  const controlled = value !== undefined;
  const [internal, setInternal] = useState(defaultValue ?? '');
  const current = controlled ? value ?? '' : internal;
  const selected = parseTime(current);
  const [open, setOpen] = useState(false);
  const [hour, setHour] = useState(selected?.hour ?? '09');
  const [minute, setMinute] = useState(selected?.minute ?? '00');
  const [position, setPosition] = useState({ left: 8, top: 8, width: 280 });
  const labels = language === 'ar'
    ? { choose: 'اختر الوقت', now: 'الآن', clear: 'مسح', done: 'تم', hour: 'الساعة', minute: 'الدقيقة' }
    : { choose: 'Choose time', now: 'Now', clear: 'Clear', done: 'Done', hour: 'Hour', minute: 'Minute' };

  useEffect(() => {
    const next = parseTime(current);
    if (next) {
      setHour(next.hour);
      setMinute(next.minute);
    }
  }, [current]);

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
      const width = Math.min(280, window.innerWidth - 16);
      const preferredLeft = isRtl ? anchor.right - width : anchor.left;
      const left = Math.max(8, Math.min(preferredLeft, window.innerWidth - width - 8));
      const estimatedHeight = 220;
      const below = anchor.bottom + 8;
      const top = below + estimatedHeight <= window.innerHeight ? below : Math.max(8, anchor.top - estimatedHeight - 8);
      setPosition({ left, top, width });
    };
    place();
    const escape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    document.addEventListener('keydown', escape);
    return () => {
      window.removeEventListener('resize', place);
      window.removeEventListener('scroll', place, true);
      document.removeEventListener('keydown', escape);
    };
  }, [isRtl, open]);

  const emit = (next: string, close = true) => {
    if (!controlled) setInternal(next);
    onChange?.(next);
    if (close) setOpen(false);
  };

  const apply = (nextHour = hour, nextMinute = minute, close = true) => {
    emit(`${nextHour}:${nextMinute}`, close);
  };

  const picker = open
    ? createPortal(
        <div
          className="fixed z-[100] rounded-2xl border border-mis-border bg-white p-4 shadow-2xl"
          dir={isRtl ? 'rtl' : 'ltr'}
          ref={popup}
          style={{ left: position.left, top: position.top, width: position.width }}
        >
          <div className="flex items-center gap-3 rounded-xl bg-slate-50 p-3">
            <Clock3 className="h-5 w-5 shrink-0 text-mis-primary" />
            <div className="ms-auto flex items-center gap-1" dir="ltr">
              <ProfessionalSelect aria-label={labels.hour} className="h-11 min-h-11 w-[5.5rem] rounded-xl px-2 text-sm" onChange={(event) => setHour(event.target.value)} value={hour}>
                {hours.map((item) => <option key={item} value={item}>{item}</option>)}
              </ProfessionalSelect>
              <span className="font-bold text-slate-500">:</span>
              <ProfessionalSelect aria-label={labels.minute} className="h-11 min-h-11 w-[5.5rem] rounded-xl px-2 text-sm" onChange={(event) => setMinute(event.target.value)} value={minute}>
                {minutes.map((item) => <option key={item} value={item}>{item}</option>)}
              </ProfessionalSelect>
            </div>
          </div>
          <div className="mt-4 flex items-center gap-2 border-t border-mis-border pt-3">
            <button
              className="min-h-9 rounded-lg px-3 py-2 text-xs font-bold text-mis-primary hover:bg-mis-pale"
              onClick={() => {
                const now = new Date();
                const nextHour = pad(now.getHours());
                const nextMinute = pad(now.getMinutes());
                setHour(nextHour);
                setMinute(nextMinute);
                apply(nextHour, nextMinute);
              }}
              type="button"
            >
              {labels.now}
            </button>
            <button className="min-h-9 rounded-lg px-3 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100" onClick={() => emit('')} type="button">
              {labels.clear}
            </button>
            <button className="ms-auto min-h-9 rounded-lg bg-mis-primary px-4 py-2 text-xs font-bold text-white" onClick={() => apply()} type="button">
              {labels.done}
            </button>
          </div>
        </div>,
        document.body,
      )
    : null;

  return (
    <div className={`relative ${className}`} ref={root}>
      <input aria-label={ariaLabel ?? labels.choose} className="pointer-events-none absolute h-px w-px opacity-0" name={name} onInvalid={() => setOpen(true)} readOnly required={required} tabIndex={-1} type="text" value={current} />
      <div className={`field flex min-h-11 items-center gap-2 p-1 ps-3 ${disabled ? 'bg-slate-100 opacity-70' : ''}`}>
        <Clock3 className="h-5 w-5 shrink-0 text-mis-primary" />
        <button
          aria-expanded={open}
          aria-label={ariaLabel ?? labels.choose}
          className={`min-h-9 min-w-0 flex-1 truncate text-start text-sm outline-none ${selected ? 'text-slate-800' : 'text-slate-500'}`}
          disabled={disabled}
          dir="ltr"
          id={id}
          onClick={() => setOpen((currentOpen) => !currentOpen)}
          type="button"
        >
          {selected ? `${selected.hour}:${selected.minute}` : labels.choose}
        </button>
        {selected && !disabled ? (
          <button aria-label={labels.clear} className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-rose-600" onClick={() => emit('')} type="button">
            <X className="h-4 w-4" />
          </button>
        ) : null}
      </div>
      {picker}
    </div>
  );
}
