import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../../components/common/PageHeader';
import { Card } from '../../components/common/Card';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { apiClient, getApiErrorMessage } from '../../services/apiClient';
import { formatExcuseTime, type ExcuseItem } from '../../features/hr/services/hrExcuseService';

export function HrOriginalVisitPage() {
  const { visitId } = useParams(); const { language, isRtl, ct } = useCollectionsLocalization(); const ar = language === 'ar';
  const [visit, setVisit] = useState<ExcuseItem>(); const [error, setError] = useState(''); const [loading, setLoading] = useState(true);
  function load() {
    if (!visitId) return;
    setLoading(true); setVisit(undefined); setError('');
    void apiClient.get<ExcuseItem>(`/hr/excuses/visits/${visitId}`).then(r => { setVisit(r.data); }).catch(e => { setError(getApiErrorMessage(e, ar ? 'تعذر تحميل الزيارة الأصلية. تحقق من الاتصال ثم أعد المحاولة.' : 'Unable to load the original visit. Check your connection and try again.')); }).finally(() => setLoading(false));
  }
  useEffect(() => { load(); }, [visitId, ar]);
  const fields = visit ? [[ar ? 'الموظف' : 'Employee', visit.employeeName], [ar ? 'رقم الموظف' : 'Employee Number', visit.employeeNumber], [ar ? 'تاريخ الزيارة' : 'Visit Date', visit.date], [ar ? 'وقت الزيارة' : 'Visit Time', formatExcuseTime(visit.fromTime, language)], [ar ? 'العميل' : 'Customer', visit.customer], [ar ? 'رقم القضية' : 'Case Number', visit.caseReference], [ar ? 'البنك / الشركة' : 'Bank / Company', visit.organization], [ar ? 'العنوان' : 'Address', visit.address], [ar ? 'حالة الزيارة' : 'Visit Status', visit.visitStatus ? ct(visit.visitStatus) : null], [ar ? 'النتيجة' : 'Result', visit.visitResult ? ct(visit.visitResult) : null], [ar ? 'الملاحظات' : 'Notes', visit.visitNotes], [ar ? 'أنشئ بواسطة' : 'Created By', visit.visitCreatedBy]] : [];
  return (
    <div dir={isRtl ? 'rtl' : 'ltr'} className="space-y-5">
      <PageHeader title={ar ? 'الزيارة الأصلية' : 'Original Field Visit'} />
      <Link to={visit ? `/hr/excuses?requestId=${visit.id}` : '/hr/excuses'} className="inline-flex h-10 items-center rounded-xl border border-mis-border bg-white px-4 text-sm font-semibold text-mis-primary hover:bg-mis-pale">{ar ? 'العودة إلى الطلب' : 'Back to Request'}</Link>
      {loading ? <div className="flex min-h-64 items-center justify-center"><LoadingSpinner /></div> : error ? <ErrorState compact message={error} onRetry={load} title={ar ? 'تعذر تحميل الزيارة' : 'Unable to load visit'} /> : (
        <Card className="p-5"><dl className="grid gap-5 sm:grid-cols-2">{fields.map(([k, v]) => <div key={String(k)}><dt className="text-sm text-slate-500">{k}</dt><dd className="whitespace-pre-wrap break-words font-medium text-mis-navy">{v || '—'}</dd></div>)}</dl></Card>
      )}
    </div>
  );
}
