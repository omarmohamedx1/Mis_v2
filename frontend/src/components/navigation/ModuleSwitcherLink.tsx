import { LayoutGrid } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { getAccessibleModules } from '../../features/modules/moduleAccess';

interface ModuleSwitcherLinkProps {
  collapsed?: boolean;
  dark?: boolean;
}

export function ModuleSwitcherLink({ collapsed = false, dark = false }: ModuleSwitcherLinkProps) {
  const { user } = useAuth();
  const { language } = useLocalization();
  if (!user || getAccessibleModules(user).length < 2) return null;

  const label = language === 'ar' ? 'تبديل الموديول' : 'Switch module';
  return (
    <Link
      to="/modules"
      title={collapsed ? label : undefined}
      className={`module-sidebar-link mb-1 flex min-h-10 items-center gap-3 rounded-xl px-3 py-2 text-sm font-semibold transition ${collapsed ? 'lg:justify-center lg:px-2' : ''} ${dark ? 'text-white/70 hover:bg-white/10 hover:text-white' : 'text-slate-600 hover:bg-white hover:text-mis-primary'}`}
    >
      <LayoutGrid className="h-5 w-5 shrink-0" />
      <span className={`module-sidebar-label min-w-0 truncate ${collapsed ? 'lg:hidden' : ''}`}>{label}</span>
    </Link>
  );
}
