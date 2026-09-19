import { AlertTriangle, Bell } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../../context/AuthContext';
import { hasHrFeature } from '../../modules/moduleAccess';
import { useLocalization } from '../../../context/LocalizationContext';
import { formatExcuseTime, hrExcuseService, type ExcuseItem } from '../services/hrExcuseService';

export function HrExcuseNotifications() {
  const { user } = useAuth(); const { language } = useLocalization(); const ar = language === 'ar';
  const allowed = hasHrFeature(user, ['hr.excuses.approve'], ['HrManager']);
  const [items, setItems] = useState<ExcuseItem[]>([]); const [open, setOpen] = useState(false); const [failed, setFailed] = useState(false); const [loading, setLoading] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!allowed) { setItems([]); return; }
    let active = true;
    const load = async () => {
      setLoading(true);
      try {
        const result = await hrExcuseService.notifications();
        if (active) { setItems(result); setFailed(false); }
      } catch {
        if (active) { setItems([]); setFailed(true); }
      } finally {
        if (active) setLoading(false);
      }
    };
    void load(); const timer = window.setInterval(() => void load(), 60000); window.addEventListener('hr-excuses-changed', load);
    return () => { active = false; clearInterval(timer); window.removeEventListener('hr-excuses-changed', load); };
  }, [allowed, user?.id, language]);
  useEffect(() => {
    function close(event: MouseEvent) { if (!root.current?.contains(event.target as Node)) setOpen(false); }
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);
  if (!allowed) return null;
  return (
    <div className="relative" ref={root}>
      <button type="button" aria-expanded={open} aria-label={ar ? 'زيارات تحتاج موافقة' : 'Pending visit approvals'} onClick={() => setOpen(!open)} className="relative flex h-10 items-center gap-2 rounded-xl border border-sky-200 bg-sky-50 px-3 text-sky-800 hover:bg-sky-100">
        <Bell className="h-5 w-5" />
        <span className={`inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-xs font-black ${failed ? 'bg-red-600 text-white' : items.length ? 'bg-mis-primary text-white' : 'bg-white text-slate-500'}`}>
          {failed ? <AlertTriangle className="h-3.5 w-3.5" aria-hidden="true" /> : items.length}
        </span>
      </button>
      {open && (
        <div className="absolute end-0 z-50 mt-2 max-h-96 w-80 max-w-[85vw] overflow-y-auto rounded-xl border border-mis-border bg-white p-3 shadow-xl">
          {failed ? (
            <div className="space-y-3 p-3" role="alert">
              <p className="text-sm text-red-700">{ar ? 'تعذر تحميل تنبيهات الزيارات. تحقق من الاتصال ثم أعد المحاولة.' : 'Unable to load visit notifications. Check your connection and try again.'}</p>
              <button
                className="inline-flex h-9 items-center rounded-lg bg-mis-primary px-3 text-xs font-bold text-white disabled:opacity-60"
                disabled={loading}
                onClick={() => window.dispatchEvent(new Event('hr-excuses-changed'))}
                type="button"
              >
                {loading ? (ar ? 'جارٍ التحميل...' : 'Loading...') : (ar ? 'إعادة المحاولة' : 'Try again')}
              </button>
            </div>
          ) : !items.length ? <p className="p-3 text-sm text-slate-500">{ar ? 'لا توجد زيارات تحتاج موافقة اليوم' : 'No pending visits today'}</p> : items.map(x => (
            <Link key={x.id} to={`/hr/excuses?requestId=${x.id}`} onClick={() => setOpen(false)} className="block space-y-1 rounded-lg border-b border-slate-100 p-3 text-sm last:border-0 hover:bg-sky-50">
              <p className="font-semibold text-mis-navy">{ar ? `${x.employeeName} لديه زيارة ميدانية اليوم` : `${x.employeeName} has a field visit scheduled today.`}</p>
              <p className="text-slate-500">{x.employeeNumber || '—'} · {x.date} · {formatExcuseTime(x.fromTime, language) || '—'}</p>
              <p>{x.customer} · {x.caseReference}</p>
              <p className="text-slate-600">{x.organization}</p>
              <p className="text-slate-500">{x.address}</p>
              <p className="font-semibold text-sky-700">{ar ? 'مراجعة الطلب' : 'Review Request'}</p>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
