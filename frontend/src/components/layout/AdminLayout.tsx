import { Activity, Gauge, ShieldCheck, Users } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const copy = {
  ar: { center: 'إدارة النظام', subtitle: 'الهوية والصلاحيات والرقابة', dashboard: 'ملخص الإدارة', users: 'المستخدمون والصلاحيات', audit: 'سجل الإدارة', profile: 'ملفي الشخصي', language: 'English', signout: 'تسجيل الخروج', open: 'فتح القائمة', close: 'إغلاق القائمة', collapse: 'طي القائمة', expand: 'توسيع القائمة', navigation: 'قائمة إدارة النظام', company: 'حوكمة نظام MIS', controlled: 'وصول محكوم' },
  en: { center: 'System Administration', subtitle: 'Identity, access & oversight', dashboard: 'Administration overview', users: 'Users & access', audit: 'Administration audit', profile: 'My profile', language: 'العربية', signout: 'Sign out', open: 'Open navigation', close: 'Close navigation', collapse: 'Collapse navigation', expand: 'Expand navigation', navigation: 'System administration navigation', company: 'MIS Governance', controlled: 'Controlled access' },
};

export function AdminLayout() {
  const { language, isRtl, setLanguage } = useLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const t = copy[language];
  const navigation: ModuleNavigationItem[] = [{ to: '/admin/dashboard', label: t.dashboard, icon: Gauge }, { to: '/admin/users', label: t.users, icon: Users }, { to: '/admin/audit', label: t.audit, icon: Activity }];
  const badge = <span className="hidden items-center rounded-full bg-violet-50 px-3 py-1.5 text-xs font-bold text-violet-700 sm:inline-flex"><ShieldCheck className="me-1 h-4 w-4" />{t.controlled}</span>;
  return <ModuleLayoutShell collapseLabel={t.collapse} companyLabel={t.company} expandLabel={t.expand} headerAside={badge} headerTitle={t.center} isRtl={isRtl} languageLabel={t.language} moduleName={t.center} moduleSubtitle={t.subtitle} navigation={navigation} navigationLabel={t.navigation} onLanguageToggle={() => setLanguage(language === 'ar' ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={t.open} closeNavigationLabel={t.close} profileLabel={t.profile} profilePath="/admin/profile" signOutLabel={t.signout} storageKey="mis.admin.sidebar" theme="admin" userName={user?.fullName} />;
}
