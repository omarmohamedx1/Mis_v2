import { Banknote, BarChart3, BookOpenText, CalendarRange, Car, LayoutDashboard, Percent, ReceiptText, Scale, ShieldCheck, UsersRound, Wallet, WalletCards } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

export function FinanceLayout() {
  const { language, isRtl, setLanguage } = useLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const ar = language === 'ar';
  const finance = ar ? 'المالية' : 'Finance';
  const payroll = ar ? 'المرتبات والعمولات' : 'Payroll & commissions';
  const navigation: ModuleNavigationItem[] = [
    { to: '/finance/dashboard', icon: LayoutDashboard, label: ar ? 'لوحة المالية' : 'Finance dashboard', group: finance },
    { to: '/finance/collections', icon: ReceiptText, label: ar ? 'التحصيلات المالية' : 'Financial collections', group: finance },
    { to: '/finance/custody', icon: WalletCards, label: ar ? 'عهد المحصلين' : 'Collector custody', group: finance },
    { to: '/finance/journals', icon: BookOpenText, label: ar ? 'القيود اليومية' : 'Journals', group: finance },
    { to: '/finance/accounts', icon: Scale, label: ar ? 'دليل الحسابات' : 'Chart of accounts', group: finance },
    { to: '/finance/periods', icon: CalendarRange, label: ar ? 'الفترات والإقفال' : 'Periods & close', group: finance },
    { to: '/finance/reports', icon: BarChart3, label: ar ? 'التقارير المالية' : 'Financial reports', group: finance },
    { to: '/finance/salaries', icon: Wallet, label: ar ? 'المرتبات' : 'Salaries', group: payroll },
    { to: '/finance/transportation', icon: Car, label: ar ? 'الانتقالات' : 'Transportation', group: payroll },
    { to: '/finance/collector-commissions', icon: Banknote, label: ar ? 'عمولات المحصلين' : 'Collector commissions', group: payroll },
    { to: '/finance/supervisor-commissions', icon: UsersRound, label: ar ? 'عمولات المشرفين' : 'Supervisor commissions', group: payroll },
    { to: '/finance/commission-rules', icon: Percent, label: ar ? 'قواعد العمولة' : 'Commission rules', group: payroll },
  ];
  const controlled = <span className="hidden items-center rounded-full bg-emerald-50 px-3 py-1.5 text-xs font-bold text-emerald-700 sm:inline-flex"><ShieldCheck className="me-1 h-4 w-4" />{ar ? 'دفاتر خاضعة للرقابة' : 'Controlled books'}</span>;
  return <ModuleLayoutShell collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'} companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'} expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'} headerAside={controlled} headerTitle={ar ? 'المحاسبة والمالية' : 'Accounting & Finance'} isRtl={isRtl} languageLabel={ar ? 'English' : 'العربية'} moduleName={ar ? 'المحاسبة والمالية' : 'MIS Finance'} moduleSubtitle={ar ? 'الدفاتر والمرتبات والعمولات' : 'Ledgers, payroll, and commissions'} navigation={navigation} navigationLabel={ar ? 'قائمة المحاسبة والمالية' : 'Finance navigation'} onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={ar ? 'فتح القائمة' : 'Open navigation'} closeNavigationLabel={ar ? 'إغلاق القائمة' : 'Close navigation'} profileLabel={ar ? 'ملفي الشخصي' : 'My profile'} profilePath="/finance/profile" signOutLabel={ar ? 'تسجيل الخروج' : 'Sign out'} storageKey="mis.finance.sidebar" theme="finance" userName={user?.fullName} />;
}
