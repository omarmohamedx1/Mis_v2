import { ArrowUpRight, BookOpenText, Building2, CheckCircle2, Languages, LayoutGrid, LogOut, ShieldCheck, UsersRound } from 'lucide-react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import misLogo from '../../assets/mis-logo.svg';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { getAccessibleModules, type ModuleId } from '../../features/modules/moduleAccess';

const LAST_MODULE_KEY = 'mis.last-module';

const modulePresentation: Record<ModuleId, {
  icon: typeof LayoutGrid;
  titleAr: string;
  titleEn: string;
  descriptionAr: string;
  descriptionEn: string;
  featuresAr: string[];
  featuresEn: string[];
  accent: string;
  iconStyle: string;
}> = {
  finance: {
    icon: BookOpenText,
    titleAr: 'المحاسبة والمالية',
    titleEn: 'Accounting & Finance',
    descriptionAr: 'القيود، دليل الحسابات، الفترات والتقارير المالية من مصدر موحّد.',
    descriptionEn: 'Controlled journals, chart of accounts, periods, and financial reporting.',
    featuresAr: ['دفتر الأستاذ', 'الفترات', 'التقارير'],
    featuresEn: ['General ledger', 'Periods', 'Reports'],
    accent: 'from-cyan-500 to-blue-700',
    iconStyle: 'bg-cyan-50 text-cyan-700 ring-cyan-100',
  },
  collections: {
    icon: Building2,
    titleAr: 'التحصيل والبنوك',
    titleEn: 'Collections & Banks',
    descriptionAr: 'إدارة المحافظ والحالات والتحصيلات والمتابعات والبنوك والعملاء.',
    descriptionEn: 'Manage portfolios, cases, collections, follow-ups, banks, and clients.',
    featuresAr: ['الحالات', 'التحصيلات', 'البنوك'],
    featuresEn: ['Cases', 'Payments', 'Banks'],
    accent: 'from-sky-500 to-indigo-700',
    iconStyle: 'bg-blue-50 text-blue-700 ring-blue-100',
  },
  hr: {
    icon: UsersRound,
    titleAr: 'الموارد البشرية',
    titleEn: 'Human Resources',
    descriptionAr: 'بيانات الموظفين والحضور والإجازات والمستندات والتقارير.',
    descriptionEn: 'Employees, attendance, leave, documents, and workforce reporting.',
    featuresAr: ['الموظفون', 'الحضور', 'الإجازات'],
    featuresEn: ['Employees', 'Attendance', 'Leave'],
    accent: 'from-emerald-500 to-teal-700',
    iconStyle: 'bg-emerald-50 text-emerald-700 ring-emerald-100',
  },
  admin: {
    icon: ShieldCheck,
    titleAr: 'إدارة النظام',
    titleEn: 'System Administration',
    descriptionAr: 'المستخدمون والصلاحيات والرقابة وسجل قرارات الإدارة.',
    descriptionEn: 'Users, access control, governance, and administration audit.',
    featuresAr: ['المستخدمون', 'الصلاحيات', 'الرقابة'],
    featuresEn: ['Users', 'Access', 'Audit'],
    accent: 'from-slate-600 to-slate-900',
    iconStyle: 'bg-slate-100 text-slate-700 ring-slate-200',
  },
};

export function ModuleSelectorPage() {
  const { user, logout } = useAuth();
  const { language, setLanguage } = useLocalization();
  const navigate = useNavigate();
  if (!user) return <Navigate to="/login" replace />;

  const modules = getAccessibleModules(user);
  if (!modules.length) return <Navigate to="/unauthorized" replace />;
  const ar = language === 'ar';
  const lastModule = localStorage.getItem(LAST_MODULE_KEY);
  const signOut = () => { logout(); navigate('/login', { replace: true }); };

  return (
    <div className="relative min-h-screen overflow-hidden bg-[#f4f7fb] text-slate-900">
      <div className="pointer-events-none absolute inset-x-0 top-0 h-[420px] bg-gradient-to-br from-[#071d35] via-[#0a4569] to-[#107ca2]" />
      <div className="pointer-events-none absolute -top-28 end-[8%] h-80 w-80 rounded-full bg-cyan-300/15 blur-3xl" />
      <div className="pointer-events-none absolute top-24 start-[3%] h-56 w-56 rounded-full bg-blue-300/10 blur-3xl" />

      <header className="relative z-10 border-b border-white/10">
        <div className="mx-auto flex max-w-7xl items-center gap-4 px-5 py-5 sm:px-8">
          <span className="grid h-14 w-14 place-items-center rounded-2xl bg-white shadow-lg shadow-slate-950/15"><img src={misLogo} alt="MIS" className="h-10 w-auto" /></span>
          <div className="text-white"><p className="font-bold tracking-wide">MIS Collection Firm</p><p className="mt-0.5 text-xs text-cyan-100/75">Enterprise Operations Platform</p></div>
          <div className="ms-auto flex items-center gap-2">
            <button type="button" onClick={() => setLanguage(ar ? 'en' : 'ar')} className="inline-flex h-11 items-center gap-2 rounded-xl border border-white/15 bg-white/10 px-3 text-sm font-semibold text-white backdrop-blur hover:bg-white/15"><Languages className="h-4 w-4" /><span className="hidden sm:inline">{ar ? 'English' : 'العربية'}</span></button>
            <button type="button" onClick={signOut} className="inline-flex h-11 items-center gap-2 rounded-xl border border-white/15 px-3 text-sm font-semibold text-white/80 hover:bg-white/10 hover:text-white"><LogOut className="h-4 w-4" /><span className="hidden sm:inline">{ar ? 'خروج' : 'Sign out'}</span></button>
          </div>
        </div>
      </header>

      <main className="relative z-10 mx-auto max-w-7xl px-5 pb-12 pt-12 sm:px-8 sm:pt-16">
        <section className="max-w-3xl text-white">
          <div className="inline-flex items-center gap-2 rounded-full border border-cyan-200/20 bg-cyan-200/10 px-3 py-1.5 text-xs font-bold uppercase tracking-[.16em] text-cyan-100"><LayoutGrid className="h-4 w-4" />{ar ? 'مساحة العمل' : 'WORKSPACE'}</div>
          <h1 className="mt-5 text-3xl font-black tracking-tight sm:text-5xl">{ar ? `مرحبًا، ${user.fullName}` : `Welcome, ${user.fullName}`}</h1>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-slate-200 sm:text-base">{ar ? 'اختر الموديول الذي تريد العمل عليه. تظهر هنا فقط مساحات العمل المصرّح لك باستخدامها، ويمكنك التبديل بينها في أي وقت.' : 'Choose where you want to work. Only authorized modules appear here, and you can switch between them at any time.'}</p>
        </section>

        <section className="mt-10 grid gap-5 md:grid-cols-2 xl:grid-cols-4" aria-label={ar ? 'الموديولات المتاحة' : 'Available modules'}>
          {modules.map(module => {
            const presentation = modulePresentation[module.id];
            const Icon = presentation.icon;
            const isLast = lastModule === module.id;
            const features = ar ? presentation.featuresAr : presentation.featuresEn;
            return (
              <Link key={module.id} to={module.homePath} onClick={() => localStorage.setItem(LAST_MODULE_KEY, module.id)} className="group relative flex min-h-[330px] flex-col overflow-hidden rounded-3xl border border-white/70 bg-white p-6 shadow-[0_20px_55px_rgba(15,23,42,.12)] transition duration-200 hover:-translate-y-1 hover:shadow-[0_26px_70px_rgba(15,23,42,.18)]">
                <span className={`absolute inset-x-0 top-0 h-1.5 bg-gradient-to-r ${presentation.accent}`} />
                <div className="flex items-start justify-between gap-3">
                  <span className={`grid h-14 w-14 place-items-center rounded-2xl ring-1 ${presentation.iconStyle}`}><Icon className="h-7 w-7" /></span>
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-[11px] font-bold text-emerald-700"><CheckCircle2 className="h-3.5 w-3.5" />{ar ? 'متاح' : 'Available'}</span>
                </div>
                <h2 className="mt-6 text-xl font-black text-[#0b2b45]">{ar ? presentation.titleAr : presentation.titleEn}</h2>
                <p className="mt-3 min-h-[72px] text-sm leading-6 text-slate-500">{ar ? presentation.descriptionAr : presentation.descriptionEn}</p>
                <div className="mt-4 flex flex-wrap gap-2">{features.map(feature => <span key={feature} className="rounded-lg bg-slate-50 px-2.5 py-1 text-xs font-semibold text-slate-600">{feature}</span>)}</div>
                <div className="mt-auto flex items-center justify-between border-t border-slate-100 pt-5"><span className="text-sm font-bold text-mis-primary">{isLast ? (ar ? 'آخر موديول مستخدم' : 'Last used module') : (ar ? 'فتح الموديول' : 'Open module')}</span><span className="grid h-9 w-9 place-items-center rounded-xl bg-slate-100 text-slate-500 transition group-hover:bg-mis-primary group-hover:text-white"><ArrowUpRight className={`h-4 w-4 ${ar ? '-rotate-90' : ''}`} /></span></div>
              </Link>
            );
          })}
        </section>

        <footer className="mt-8 flex flex-col gap-3 rounded-2xl border border-slate-200/80 bg-white/80 px-5 py-4 text-sm text-slate-500 shadow-sm backdrop-blur sm:flex-row sm:items-center">
          <div className="flex items-center gap-2"><ShieldCheck className="h-5 w-5 text-emerald-600" /><span>{ar ? 'الوصول محكوم بالصلاحيات والنطاقات المعتمدة.' : 'Access is controlled by your approved permissions and scopes.'}</span></div>
          <div className="sm:ms-auto" dir="ltr"><span className="font-semibold text-slate-700">{user.loginCode}</span> · {user.department}</div>
        </footer>
      </main>
    </div>
  );
}
