import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../../components/common/PageHeader';
import { Card } from '../../components/common/Card';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { apiClient, getApiErrorMessage } from '../../services/apiClient';
import type { ExcuseItem } from '../../features/hr/services/hrExcuseService';

export function HrOriginalVisitPage() {
  const { visitId } = useParams(); const { language, isRtl, ct } = useCollectionsLocalization(); const ar = language === 'ar';
  const [visit, setVisit] = useState<ExcuseItem>(); const [error, setError] = useState('');
  useEffect(() => { let active = true; setVisit(undefined); setError(''); void apiClient.get<ExcuseItem>(`/hr/excuses/visits/${visitId}`).then(r => { if (active) setVisit(r.data); }).catch(e => { if (active) setError(getApiErrorMessage(e, ar ? 'تعذر تحميل الزيارة' : 'Unable to load visit')); }); return () => { active = false; }; }, [visitId, ar]);
  const fields = visit ? [[ar ? 'الموظف' : 'Employee', visit.employeeName], [ar ? 'رقم الموظف' : 'Employee Number', visit.employeeNumber], [ar ? 'تاريخ الزيارة' : 'Visit Date', visit.date], [ar ? 'وقت الزيارة' : 'Visit Time', visit.fromTime], [ar ? 'العميل' : 'Customer', visit.customer], [ar ? 'رقم القضية' : 'Case Number', visit.caseReference], [ar ? 'البنك / الشركة' : 'Bank / Company', visit.organization], [ar ? 'العنوان' : 'Address', visit.address], [ar ? 'حالة الزيارة' : 'Visit Status', visit.visitStatus ? ct(visit.visitStatus) : null], [ar ? 'النتيجة' : 'Result', visit.visitResult ? ct(visit.visitResult) : null], [ar ? 'الملاحظات' : 'Notes', visit.visitNotes], [ar ? 'أنشئ بواسطة' : 'Created By', visit.visitCreatedBy]] : [];
  return <div dir={isRtl ? 'rtl' : 'ltr'} className="space-y-5"><PageHeader title={ar ? 'الزيارة الأصلية' : 'Original Field Visit'} /><Link to={visit ? `/hr/excuses?requestId=${visit.id}` : '/hr/excuses'} className="text-mis-primary underline">{ar ? 'العودة إلى الطلب' : 'Back to Request'}</Link>{error && <p role="alert">{error}</p>}<Card className="p-5"><dl className="grid gap-5 sm:grid-cols-2">{fields.map(([k, v]) => <div key={k}><dt className="text-sm text-slate-500">{k}</dt><dd className="whitespace-pre-wrap">{v || '—'}</dd></div>)}</dl></Card></div>;
}
