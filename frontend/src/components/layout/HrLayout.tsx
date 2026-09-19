import { HrExcuseNotifications } from '../../features/hr/components/HrExcuseNotifications';
import { Activity, BarChart3, CalendarCheck2, CalendarDays, Database, FileText, Gauge, Plane, ScrollText, ShieldCheck, UsersRound, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import type { TranslationKey } from '../../localization/translations';
import { hasHrFeature } from '../../features/modules/moduleAccess';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  ['/hr/dashboard', 'dashboard', Gauge, 'hrNavOverview'],
  ['/hr/employees', 'employees', UsersRound, 'hrNavPeople'], ['/hr/employee-documents', 'employeeDocuments', FileText, 'hrNavPeople'], ['/hr/social-insurance', 'socialInsurance', ShieldCheck, 'hrNavPeople'],
  ['/hr/attendance', 'attendance', CalendarCheck2, 'hrNavTime'], ['/hr/excuses', 'excusesMissions', ScrollText, 'hrNavTime'], ['/hr/leaves', 'leaves', CalendarDays, 'hrNavTime'], ['/hr/delegations', 'delegations', Plane, 'hrNavTime'], ['/hr/calendar', 'workingCalendar', CalendarDays, 'hrNavTime'],
  ['/hr/payroll', 'netSalaries', Wallet, 'hrNavInsights'], ['/hr/reports', 'reports', BarChart3, 'hrNavInsights'], ['/hr/audit', 'auditHistory', Activity, 'hrNavInsights'],
  ['/hr/master', 'hrMasterNav', Database, 'hrNavSetup'],
] as const;

export function HrLayout() {
  const { isRtl, language, setLanguage, t } = useLocalization();
  const { logout, user } = useAuth();
  const navigate = useNavigate();
  const navigation: ModuleNavigationItem[] = links
    .filter(([to]) => to !== '/hr/excuses' || hasHrFeature(user, ['hr.excuses.view', 'hr.excuses.manage', 'hr.excuses.approve']))
    .filter(([to]) => to !== '/hr/social-insurance' || hasHrFeature(user, ['hr.social_insurance.view', 'hr.social_insurance.manage']))
    .map(([to, key, icon, group]) => ({ to, icon, label: t(key as TranslationKey), group: t(group) }));
  return <ModuleLayoutShell headerAside={<HrExcuseNotifications />} collapseLabel={t('collapseNavigation')} companyLabel={t('collectionFirm')} expandLabel={t('expandNavigation')} headerTitle={t('humanResources')} isRtl={isRtl} languageLabel={language === 'ar' ? t('english') : t('arabic')} moduleName={t('humanResources')} moduleSubtitle={t('hrDepartment')} navigation={navigation} navigationLabel={t('hrNavigation')} onLanguageToggle={() => setLanguage(language === 'ar' ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={t('openNavigation')} closeNavigationLabel={t('closeNavigation')} profileLabel={t('myProfile')} profilePath="/hr/profile" signOutLabel={t('logout')} storageKey="mis.hr.sidebar" theme="hr" userName={user?.fullName} />;
}
