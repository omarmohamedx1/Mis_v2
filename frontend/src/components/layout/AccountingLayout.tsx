import { Calculator, Car, LayoutDashboard, UsersRound, Wallet } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  ['/accounting/dashboard', 'لوحة التحكم', 'Dashboard', LayoutDashboard],
  ['/accounting/salaries', 'المرتبات', 'Salaries', Wallet],
  ['/accounting/transportation', 'الانتقالات', 'Transportation', Car],
  ['/accounting/collector-commissions', 'عمولات المحصلين', 'Collector commissions', Calculator],
  ['/accounting/supervisor-commissions', 'عمولات المشرفين', 'Supervisor commissions', UsersRound],
] as const;

export function AccountingLayout() {
  const { language, isRtl, setLanguage } = useLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const ar = language === 'ar';
  const navigation: ModuleNavigationItem[] = links.map(([to, arabic, english, icon]) => ({ to, icon, label: ar ? arabic : english }));

  return (
    <ModuleLayoutShell
      collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'}
      companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'}
      expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'}
      headerTitle={ar ? 'الحسابات' : 'Accounting'}
      isRtl={isRtl}
      languageLabel={ar ? 'English' : 'العربية'}
      moduleName={ar ? 'الحسابات' : 'MIS Accounting'}
      moduleSubtitle={ar ? 'المرتبات والعمولات والانتقالات' : 'Payroll, commissions, and transportation'}
      navigation={navigation}
      navigationLabel={ar ? 'قائمة الحسابات' : 'Accounting navigation'}
      onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')}
      onSignOut={() => { logout(); navigate('/login', { replace: true }); }}
      openNavigationLabel={ar ? 'فتح القائمة' : 'Open navigation'}
      closeNavigationLabel={ar ? 'إغلاق القائمة' : 'Close navigation'}
      profileLabel={ar ? 'ملفي الشخصي' : 'My profile'}
      profilePath="/accounting/profile"
      signOutLabel={ar ? 'تسجيل الخروج' : 'Sign out'}
      storageKey="mis.accounting.sidebar"
      theme="finance"
      userName={user?.fullName}
    />
  );
}
