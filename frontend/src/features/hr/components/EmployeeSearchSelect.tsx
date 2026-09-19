import { Check, ChevronDown, Search, X } from 'lucide-react';
import { useEffect, useId, useRef, useState } from 'react';
import { useLocalization } from '../../../context/LocalizationContext';
import { hrEmployeeService } from '../services/hrEmployeeService';
import type { EmployeeListItem } from '../types/employee';

interface EmployeeSearchSelectProps {
  disabled?: boolean;
  error?: string;
  includeInactive?: boolean;
  initialSelection?: Pick<EmployeeListItem, 'id' | 'employeeNumber' | 'fullName'> | null;
  label: string;
  onChange: (employeeId: string, employee: EmployeeListItem | null) => void;
  required?: boolean;
  value: string;
}

export function EmployeeSearchSelect({ disabled = false, error, includeInactive = false, initialSelection = null, label, onChange, required = false, value }: EmployeeSearchSelectProps) {
  const { language, t } = useLocalization();
  const id = useId();
  const wrapperRef = useRef<HTMLDivElement>(null);
  const previousValueRef = useRef(value);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState(initialSelection ? `${initialSelection.employeeNumber} - ${initialSelection.fullName}` : '');
  const [selectedLabel, setSelectedLabel] = useState(initialSelection ? `${initialSelection.employeeNumber} - ${initialSelection.fullName}` : '');
  const [options, setOptions] = useState<EmployeeListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState('');

  useEffect(() => {
    if (previousValueRef.current && !value && query === selectedLabel) {
      setQuery('');
      setSelectedLabel('');
    }
    previousValueRef.current = value;
  }, [query, selectedLabel, value]);

  useEffect(() => {
    if (!initialSelection) return;
    const next = `${initialSelection.employeeNumber} - ${initialSelection.fullName}`;
    setSelectedLabel(next);
    if (value === initialSelection.id) setQuery(next);
  }, [initialSelection, value]);

  useEffect(() => {
    function close(event: MouseEvent) {
      if (!wrapperRef.current?.contains(event.target as Node)) setOpen(false);
    }
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') setOpen(false);
    }
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', onKey);
    };
  }, []);

  useEffect(() => {
    if (!open) return undefined;
    const search = value && query === selectedLabel ? '' : query.trim();
    const timer = window.setTimeout(async () => {
      setLoading(true);
      setLoadError('');
      try {
        const result = await hrEmployeeService.getEmployees({ departmentId: '', includeInactive, page: 1, pageSize: 12, search, status: includeInactive ? 'all' : 'active' });
        setOptions(result.items);
      } catch {
        setOptions([]);
        setLoadError(t('employeeSearchLoadError'));
      } finally {
        setLoading(false);
      }
    }, 250);
    return () => window.clearTimeout(timer);
  }, [includeInactive, language, open, query, selectedLabel, t, value]);

  function select(employee: EmployeeListItem) {
    const next = `${employee.employeeNumber} - ${employee.fullName}`;
    setQuery(next);
    setSelectedLabel(next);
    setOpen(false);
    onChange(employee.id, employee);
  }

  function clear() {
    setQuery('');
    setSelectedLabel('');
    onChange('', null);
    setOpen(true);
  }

  return (
    <div className="space-y-2" ref={wrapperRef}>
      <label className="block text-sm font-semibold text-slate-700" htmlFor={id}>{label}{required ? <span className="ms-1 text-red-500" aria-hidden="true">*</span> : null}</label>
      <div className="relative">
        <Search className="pointer-events-none absolute start-4 top-3.5 h-5 w-5 text-slate-400" aria-hidden="true" />
        <input
          aria-autocomplete="list"
          aria-controls={`${id}-options`}
          aria-expanded={open}
          aria-invalid={Boolean(error)}
          autoComplete="off"
          className={`h-12 w-full rounded-form border bg-white pe-20 ps-11 text-sm text-mis-ink outline-none transition focus:border-mis-blue focus:shadow-input ${error ? 'border-red-400' : 'border-mis-border'}`}
          disabled={disabled}
          id={id}
          onChange={(event) => {
            setQuery(event.target.value);
            if (value) onChange('', null);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder={t('searchEmployee')}
          role="combobox"
          value={query}
        />
        {value ? <button aria-label={t('clearSelection')} className="absolute end-10 top-2.5 rounded-lg p-1.5 text-slate-400 hover:bg-slate-100" onClick={clear} type="button"><X className="h-4 w-4" /></button> : null}
        <button aria-label={t('openEmployeeList')} className="absolute end-2 top-2.5 rounded-lg p-1.5 text-slate-400 hover:bg-slate-100" disabled={disabled} onClick={() => setOpen((current) => !current)} type="button"><ChevronDown className="h-4 w-4" /></button>
        {open ? (
          <div className="absolute z-50 mt-2 max-h-64 w-full overflow-y-auto rounded-xl border border-mis-border bg-white p-1 shadow-panel" id={`${id}-options`} role="listbox">
            {loading ? <p className="px-3 py-4 text-center text-sm text-slate-500">{t('loading')}</p> : loadError ? <p className="px-3 py-4 text-center text-sm text-red-600" role="alert">{loadError}</p> : options.length === 0 ? <p className="px-3 py-4 text-center text-sm text-slate-500">{t('noEmployeesFound')}</p> : options.map((employee) => (
              <button className="flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-start text-sm hover:bg-mis-pale" key={employee.id} onClick={() => select(employee)} role="option" type="button">
                <span className="min-w-0"><span className="block truncate font-semibold text-mis-navy">{employee.fullName}</span><span className="block text-xs text-slate-500">{employee.employeeNumber} - {employee.departmentName}</span></span>
                {employee.id === value ? <Check className="h-4 w-4 flex-none text-mis-primary" aria-hidden="true" /> : null}
              </button>
            ))}
          </div>
        ) : null}
      </div>
      {error ? <p className="text-sm text-red-600" role="alert">{error}</p> : null}
    </div>
  );
}
