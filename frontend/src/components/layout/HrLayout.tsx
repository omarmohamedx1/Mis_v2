import { Activity, BarChart3, CalendarCheck2, CalendarDays, Database, FileText, Gauge, ScrollText, UserRoundX, UsersRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import type { TranslationKey } from '../../localization/translations';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  ['/hr/dashboard', 'dashboard', Gauge], ['/hr/employees', 'employees', UsersRound], ['/hr/attendance', 'attendance', CalendarCheck2],
  ['/hr/leaves', 'leaves', CalendarDays], ['/hr/delegations', 'delegations', ScrollText], ['/hr/absences', 'companyAbsences', UserRoundX],
  ['/hr/employee-documents', 'employeeDocuments', FileText], ['/hr/master', 'master', Database], ['/hr/calendar', 'workingCalendar', CalendarDays],
  ['/hr/reports', 'reports', BarChart3], ['/hr/audit', 'auditHistory', Activity],
] as const;

export function HrLayout() {
  const { isRtl, language, setLanguage, t } = useLocalization();
  const { logout, user } = useAuth();
  const navigate = useNavigate();
  const navigation: ModuleNavigationItem[] = links.map(([to, key, icon]) => ({ to, icon, label: t(key as TranslationKey) }));
  return <ModuleLayoutShell collapseLabel={language === 'ar' ? 'طي القائمة' : 'Collapse navigation'} companyLabel={t('collectionFirm')} expandLabel={language === 'ar' ? 'توسيع القائمة' : 'Expand navigation'} headerTitle={t('humanResources')} isRtl={isRtl} languageLabel={language === 'ar' ? 'English' : 'العربية'} moduleName={t('humanResources')} moduleSubtitle={t('hrDepartment')} navigation={navigation} navigationLabel={t('hrNavigation')} onLanguageToggle={() => setLanguage(language === 'ar' ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={t('openNavigation')} closeNavigationLabel={t('closeNavigation')} profileLabel={language === 'ar' ? 'ملفي الشخصي' : 'My profile'} profilePath="/hr/profile" signOutLabel={t('logout')} storageKey="mis.hr.sidebar" theme="hr" userName={user?.fullName} />;
}
