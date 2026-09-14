import { Bell } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../../context/AuthContext';
import { useLocalization } from '../../../context/LocalizationContext';
import { hrExcuseService, type ExcuseItem } from '../services/hrExcuseService';

export function HrExcuseNotifications() {
  const { user } = useAuth(); const { language } = useLocalization(); const ar = language === 'ar';
  const allowed = user?.roles.includes('HrManager') || user?.permissions.includes('hr.excuses.approve');
  const [items, setItems] = useState<ExcuseItem[]>([]); const [open, setOpen] = useState(false); const [failed, setFailed] = useState(false);
  useEffect(() => {
    if (!allowed) { setItems([]); return; }
    let active = true;
    const load = async () => { try { const result = await hrExcuseService.notifications(); if (active) { setItems(result); setFailed(false); } } catch { if (active) { setItems([]); setFailed(true); } } };
    void load(); const timer = window.setInterval(() => void load(), 60000); window.addEventListener('hr-excuses-changed', load);
    return () => { active = false; clearInterval(timer); window.removeEventListener('hr-excuses-changed', load); };
  }, [allowed, user?.id, language]);
  if (!allowed) return null;
  return <div className="relative"><button type="button" aria-expanded={open} aria-label={ar ? 'زيارات تحتاج موافقة' : 'Pending visit approvals'} onClick={() => setOpen(!open)} className="flex items-center gap-2 rounded-lg p-2 text-sky-800"><Bell className="h-5 w-5" /><span>{failed ? '!' : items.length}</span></button>{open && <div className="absolute end-0 z-50 mt-2 max-h-96 w-80 max-w-[85vw] overflow-y-auto rounded-xl border bg-white p-3 shadow-xl">{failed ? <p>{ar ? 'تعذر تحميل التنبيهات' : 'Unable to load notifications'}</p> : !items.length ? <p className="p-3 text-sm text-slate-500">{ar ? 'لا توجد زيارات تحتاج موافقة اليوم' : 'No pending visits today'}</p> : items.map(x => <Link key={x.id} to={`/hr/excuses?requestId=${x.id}`} onClick={() => setOpen(false)} className="block space-y-1 border-b p-3 text-sm last:border-0 hover:bg-sky-50"><p className="font-semibold">{ar ? `${x.employeeName} لديه زيارة ميدانية اليوم` : `${x.employeeName} has a field visit scheduled today.`}</p><p>{x.employeeNumber || '—'} · {x.date} · {x.fromTime?.slice(0, 5)}</p><p>{x.customer} · {x.caseReference}</p><p>{x.organization}</p><p>{x.address}</p><p className="font-semibold text-sky-700">{ar ? 'مراجعة الطلب' : 'Review Request'}</p></Link>)}</div>}</div>;
}
