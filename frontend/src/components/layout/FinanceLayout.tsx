import { BarChart3, BookOpenText, CalendarRange, LayoutDashboard, ReceiptText, Scale, ShieldCheck, WalletCards } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  ['/finance/dashboard', 'لوحة المالية', 'Finance dashboard', LayoutDashboard], ['/finance/collections', 'التحصيلات المالية', 'Financial collections', ReceiptText],
  ['/finance/custody', 'عهد المحصلين', 'Collector custody', WalletCards], ['/finance/journals', 'القيود اليومية', 'Journals', BookOpenText],
  ['/finance/accounts', 'دليل الحسابات', 'Chart of accounts', Scale], ['/finance/periods', 'الفترات والإقفال', 'Periods & close', CalendarRange],
  ['/finance/reports', 'التقارير المالية', 'Financial reports', BarChart3],
] as const;

export function FinanceLayout() {
  const { language, isRtl, setLanguage } = useLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const ar = language === 'ar';
  const navigation: ModuleNavigationItem[] = links.map(([to, arabic, english, icon]) => ({ to, icon, label: ar ? arabic : english }));
  const controlled = <span className="hidden items-center rounded-full bg-emerald-50 px-3 py-1.5 text-xs font-bold text-emerald-700 sm:inline-flex"><ShieldCheck className="me-1 h-4 w-4" />{ar ? 'دفاتر خاضعة للرقابة' : 'Controlled books'}</span>;
  return <ModuleLayoutShell collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'} companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'} expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'} headerAside={controlled} headerTitle={ar ? 'المحاسبة والمالية' : 'Accounting & Finance'} isRtl={isRtl} languageLabel={ar ? 'English' : 'العربية'} moduleName={ar ? 'المالية' : 'MIS Finance'} moduleSubtitle={ar ? 'كل جنيه قابل للتتبع' : 'Every pound traceable'} navigation={navigation} navigationLabel={ar ? 'قائمة المالية' : 'Finance navigation'} onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={ar ? 'فتح القائمة' : 'Open navigation'} closeNavigationLabel={ar ? 'إغلاق القائمة' : 'Close navigation'} profileLabel={ar ? 'ملفي الشخصي' : 'My profile'} profilePath="/finance/profile" signOutLabel={ar ? 'تسجيل الخروج' : 'Sign out'} storageKey="mis.finance.sidebar" theme="finance" userName={user?.fullName} />;
}
