import { LayoutDashboard, Scale } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  ['/legal/dashboard', 'لوحة التحكم', 'Dashboard', LayoutDashboard],
  ['/legal/cases', 'القضايا', 'Cases', Scale],
] as const;

export function LegalLayout() {
  const { language, isRtl, setLanguage } = useLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const ar = language === 'ar';
  const navigation: ModuleNavigationItem[] = links.map(([to, arabic, english, icon]) => ({
    to,
    icon,
    label: ar ? arabic : english,
  }));

  return (
    <ModuleLayoutShell
      collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'}
      companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'}
      expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'}
      headerTitle={ar ? 'الشؤون القانونية' : 'Legal Affairs'}
      isRtl={isRtl}
      languageLabel={ar ? 'English' : 'العربية'}
      moduleName={ar ? 'الشؤون القانونية' : 'MIS Legal'}
      moduleSubtitle={ar ? 'قضايا التحصيل المحوّلة بحالة LEGAL' : 'Collection cases referred with LEGAL status'}
      navigation={navigation}
      navigationLabel={ar ? 'قائمة الشؤون القانونية' : 'Legal navigation'}
      onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')}
      onSignOut={() => {
        logout();
        navigate('/login', { replace: true });
      }}
      openNavigationLabel={ar ? 'فتح القائمة' : 'Open navigation'}
      closeNavigationLabel={ar ? 'إغلاق القائمة' : 'Close navigation'}
      profileLabel={ar ? 'ملفي الشخصي' : 'My profile'}
      profilePath="/legal/profile"
      signOutLabel={ar ? 'تسجيل الخروج' : 'Sign out'}
      storageKey="mis.legal.sidebar"
      theme="legal"
      userName={user?.fullName}
    />
  );
}
