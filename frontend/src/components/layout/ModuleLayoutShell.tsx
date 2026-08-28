import { Languages, LogOut, Menu, PanelLeftClose, PanelLeftOpen, UserCircle, X, type LucideIcon } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import misLogo from '../../assets/mis-logo.svg';
import { ModuleSwitcherLink } from '../navigation/ModuleSwitcherLink';

export type ModuleTheme = 'admin' | 'collections' | 'finance' | 'hr';

export interface ModuleNavigationItem {
  icon: LucideIcon;
  label: string;
  to: string;
}

interface ModuleLayoutShellProps {
  collapseLabel: string;
  companyLabel: string;
  expandLabel: string;
  headerAside?: ReactNode;
  headerTitle: string;
  isRtl: boolean;
  languageLabel: string;
  moduleName: string;
  moduleSubtitle: string;
  navigation: readonly ModuleNavigationItem[];
  navigationLabel: string;
  onLanguageToggle: () => void;
  onSignOut: () => void;
  openNavigationLabel: string;
  closeNavigationLabel: string;
  profileLabel: string;
  profilePath: string;
  signOutLabel: string;
  storageKey: string;
  theme: ModuleTheme;
  userName?: string;
}

const themeClasses: Record<ModuleTheme, { active: string; badge: string; eyebrow: string; icon: string }> = {
  admin: { active: 'bg-violet-50 text-violet-800 ring-violet-100', badge: 'bg-violet-50 text-violet-700', eyebrow: 'text-violet-700', icon: 'text-violet-600' },
  collections: { active: 'bg-indigo-50 text-indigo-800 ring-indigo-100', badge: 'bg-indigo-50 text-indigo-700', eyebrow: 'text-indigo-700', icon: 'text-indigo-600' },
  finance: { active: 'bg-emerald-50 text-emerald-800 ring-emerald-100', badge: 'bg-emerald-50 text-emerald-700', eyebrow: 'text-emerald-700', icon: 'text-emerald-600' },
  hr: { active: 'bg-sky-50 text-sky-800 ring-sky-100', badge: 'bg-sky-50 text-sky-700', eyebrow: 'text-sky-700', icon: 'text-sky-600' },
};

export function ModuleLayoutShell(props: ModuleLayoutShellProps) {
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(props.storageKey) === 'collapsed');
  const colors = themeClasses[props.theme];

  useEffect(() => setOpen(false), [location.pathname]);
  useEffect(() => {
    if (!open) return undefined;
    const previousOverflow = document.body.style.overflow;
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false); };
    document.body.style.overflow = 'hidden';
    document.addEventListener('keydown', closeOnEscape);
    return () => { document.body.style.overflow = previousOverflow; document.removeEventListener('keydown', closeOnEscape); };
  }, [open]);

  const toggleCollapsed = () => setCollapsed((current) => {
    const next = !current;
    localStorage.setItem(props.storageKey, next ? 'collapsed' : 'expanded');
    return next;
  });

  return <div className="module-shell min-h-dvh bg-mis-surface text-mis-ink" data-sidebar-collapsed={collapsed} data-module-theme={props.theme}>
    {open ? <button aria-label={props.closeNavigationLabel} className="fixed inset-0 z-30 bg-slate-950/45 backdrop-blur-[1px] lg:hidden" onClick={() => setOpen(false)} type="button" /> : null}
    <aside aria-label={props.navigationLabel} style={{ insetInlineStart: 0, borderInlineEndWidth: 1 }} className={`module-sidebar fixed inset-y-0 z-40 flex w-[min(18rem,calc(100vw-3rem))] flex-col border-mis-border bg-white shadow-panel transition-[width,transform] duration-200 ease-out lg:translate-x-0 lg:shadow-sm ${open ? 'translate-x-0' : props.isRtl ? 'translate-x-full' : '-translate-x-full'}`}>
      <div className="module-sidebar-header flex h-24 shrink-0 items-center justify-between border-b border-mis-border px-5">
        <div className="module-sidebar-identity flex min-w-0 items-center gap-3"><img src={misLogo} alt="MIS" className="module-sidebar-logo h-14 w-auto shrink-0" /><div className="module-sidebar-label min-w-0"><p className="truncate text-sm font-black text-mis-navy">{props.moduleName}</p><p className="mt-0.5 truncate text-xs text-slate-500">{props.moduleSubtitle}</p></div></div>
        <div className="module-sidebar-toggle flex items-center"><button aria-label={collapsed ? props.expandLabel : props.collapseLabel} title={collapsed ? props.expandLabel : props.collapseLabel} className="hidden h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-mis-primary lg:inline-flex" onClick={toggleCollapsed} type="button">{collapsed ? <PanelLeftOpen className="h-5 w-5 rtl:rotate-180" /> : <PanelLeftClose className="h-5 w-5 rtl:rotate-180" />}</button><button aria-label={props.closeNavigationLabel} className="inline-flex h-10 w-10 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 lg:hidden" onClick={() => setOpen(false)} type="button"><X className="h-5 w-5" /></button></div>
      </div>
      <div className="module-sidebar-brand shrink-0 px-5 pb-3 pt-5"><span className={`inline-flex rounded-full px-3 py-1 text-[11px] font-black uppercase tracking-[.14em] ${colors.badge}`}>{props.companyLabel}</span></div>
      <nav className="module-sidebar-scroll min-h-0 flex-1 space-y-1 overflow-y-auto overscroll-contain px-3 py-2">
        {props.navigation.map(({ to, label, icon: Icon }) => <NavLink key={to} to={to} title={collapsed ? label : undefined} className={({ isActive }) => `module-sidebar-link group flex min-h-12 items-center gap-3 rounded-xl px-4 text-sm font-semibold ring-1 ring-transparent transition-colors ${isActive ? colors.active : 'text-slate-600 hover:bg-slate-50 hover:text-mis-navy'}`}><Icon aria-hidden="true" className={`h-5 w-5 shrink-0 ${colors.icon}`} /><span className="module-sidebar-label min-w-0 truncate">{label}</span></NavLink>)}
      </nav>
      <div className="module-sidebar-footer shrink-0 border-t border-mis-border bg-white p-3">
        <ModuleSwitcherLink collapsed={collapsed} />
        <button className="module-sidebar-link mb-1 flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold text-slate-600 hover:bg-slate-50 hover:text-mis-primary" onClick={props.onLanguageToggle} title={collapsed ? props.languageLabel : undefined} type="button"><Languages className="h-5 w-5 shrink-0" /><span className="module-sidebar-label min-w-0 truncate">{props.languageLabel}</span></button>
        <NavLink to={props.profilePath} title={collapsed ? props.profileLabel : undefined} className={({ isActive }) => `module-sidebar-link mb-1 flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 ${isActive ? colors.active : 'text-slate-600 hover:bg-slate-50'}`}><UserCircle className="h-5 w-5 shrink-0" /><div className="module-sidebar-label min-w-0"><p className="truncate text-sm font-semibold text-mis-navy">{props.userName || '—'}</p><p className="truncate text-xs text-slate-500">{props.profileLabel}</p></div></NavLink>
        <button className="module-sidebar-link flex min-h-11 w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold text-slate-600 hover:bg-rose-50 hover:text-rose-700" onClick={props.onSignOut} title={collapsed ? props.signOutLabel : undefined} type="button"><LogOut className="h-5 w-5 shrink-0" /><span className="module-sidebar-label">{props.signOutLabel}</span></button>
      </div>
    </aside>
    <div className="module-content min-w-0">
      <header className="sticky top-0 z-20 flex h-[4.5rem] items-center border-b border-mis-border bg-white/95 px-4 shadow-[0_1px_0_rgba(15,23,42,.02)] backdrop-blur sm:px-6 xl:px-8"><button aria-label={props.openNavigationLabel} className="me-3 inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-mis-border bg-white text-mis-navy shadow-sm hover:bg-slate-50 lg:hidden" onClick={() => setOpen(true)} type="button"><Menu className="h-5 w-5" /></button><div className="min-w-0"><p className={`truncate text-[11px] font-black uppercase tracking-[.16em] ${colors.eyebrow}`}>{props.companyLabel}</p><p className="mt-0.5 truncate text-sm font-bold text-mis-navy sm:text-base">{props.headerTitle}</p></div>{props.headerAside ? <div className="ms-auto flex min-w-0 items-center gap-2">{props.headerAside}</div> : null}</header>
      <main className="min-w-0 p-4 sm:p-6 xl:p-8"><div className="mx-auto min-w-0 w-full max-w-[1680px]"><Outlet /></div></main>
    </div>
  </div>;
}
