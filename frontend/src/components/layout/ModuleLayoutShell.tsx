import { ChevronDown, ChevronLeft, ChevronRight, Languages, LogOut, Menu, UserCircle, X, type LucideIcon } from 'lucide-react';
import { useEffect, useId, useMemo, useRef, useState, type ReactNode } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import misLogo from '../../assets/mis-logo.svg';
import { ModuleSwitcherLink } from '../navigation/ModuleSwitcherLink';
import { useBodyScrollLock, useFocusBoundary, useMediaQuery, useOverlayLayer } from '../common/overlay';

export type ModuleTheme = 'admin' | 'collections' | 'finance' | 'hr' | 'legal';

export interface ModuleNavigationItem {
  group?: string;
  groupId?: string;
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
  showCurrentPageTitle?: boolean;
  storageKey: string;
  theme: ModuleTheme;
  userName?: string;
}

const chrome = {
  active: 'bg-slate-100 text-[#0a1e36]',
  accent: 'bg-[#0a1e36]',
  eyebrow: 'text-slate-500',
  icon: 'text-[#0a1e36]',
};

function linkClass(active: boolean) {
  return [
    'module-sidebar-link group relative flex min-h-9 items-center gap-3 rounded-lg px-3 py-2 text-[13px] font-medium transition-colors',
    active ? chrome.active : 'text-slate-600 hover:bg-slate-50 hover:text-[#0a1e36]',
  ].join(' ');
}

export function ModuleLayoutShell(props: ModuleLayoutShellProps) {
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const desktop = useMediaQuery('(min-width: 1024px)');
  const navigationId = useId();
  const menuButton = useRef<HTMLButtonElement>(null);
  const navigationRef = useRef<HTMLElement>(null);
  useBodyScrollLock(open && !desktop);
  useOverlayLayer(navigationId, open && !desktop, () => { setOpen(false); menuButton.current?.focus(); });
  useFocusBoundary(navigationId, open && !desktop, navigationRef);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(props.storageKey) === 'collapsed');
  const [closedGroups, setClosedGroups] = useState<string[]>([]);
  const groups = useMemo(() => {
    const grouped = new Map<string, { id: string; label?: string; items: ModuleNavigationItem[] }>();
    props.navigation.forEach(item => {
      const id = item.groupId ?? item.group ?? 'navigation';
      if (!grouped.has(id)) grouped.set(id, { id, label: item.group, items: [] });
      grouped.get(id)!.items.push(item);
    });
    return [...grouped.values()];
  }, [props.navigation]);
  const currentNavigationItem = [...props.navigation]
    .sort((left, right) => right.to.length - left.to.length)
    .find((item) => location.pathname === item.to || location.pathname.startsWith(`${item.to}/`));
  const CollapseIcon = collapsed
    ? (props.isRtl ? ChevronLeft : ChevronRight)
    : (props.isRtl ? ChevronRight : ChevronLeft);

  useEffect(() => {
    setOpen(false);
    const activeGroup = currentNavigationItem?.groupId ?? currentNavigationItem?.group;
    if (activeGroup) setClosedGroups(current => current.filter(group => group !== activeGroup));
  }, [location.pathname, currentNavigationItem?.groupId, currentNavigationItem?.group]);

  const toggleCollapsed = () => setCollapsed((current) => {
    const next = !current;
    localStorage.setItem(props.storageKey, next ? 'collapsed' : 'expanded');
    return next;
  });

  return (
    <div className="module-shell min-h-dvh bg-mis-surface text-mis-ink" data-sidebar-collapsed={collapsed} data-module-theme={props.theme}>
      {open ? <button aria-label={props.closeNavigationLabel} className="fixed inset-0 z-30 bg-slate-950/45 backdrop-blur-[1px] lg:hidden" onClick={() => setOpen(false)} type="button" /> : null}
      <aside
        ref={navigationRef}
        aria-label={props.navigationLabel}
        inert={!desktop && !open}
        style={props.isRtl ? { right: 0, left: 'auto', borderLeftWidth: 1 } : { left: 0, right: 'auto', borderRightWidth: 1 }}
        className={`module-sidebar fixed inset-y-0 z-40 flex w-[min(18rem,calc(100vw-2.5rem))] flex-col border-mis-border bg-white shadow-panel transition-[width,transform] duration-200 ease-out lg:translate-x-0 lg:shadow-[4px_0_24px_-12px_rgba(15,23,42,.18)] ${open ? 'translate-x-0' : props.isRtl ? 'translate-x-full' : '-translate-x-full'}`}
      >
        <button
          aria-label={collapsed ? props.expandLabel : props.collapseLabel}
          className="module-sidebar-collapse"
          onClick={toggleCollapsed}
          title={collapsed ? props.expandLabel : props.collapseLabel}
          type="button"
        >
          <CollapseIcon className="h-4 w-4" />
        </button>

        <div className="module-sidebar-header flex min-h-[4.75rem] shrink-0 items-center gap-3 border-b border-mis-border px-4 pb-3 pt-5">
          <div className="module-sidebar-identity flex min-w-0 flex-1 items-center gap-3">
            <img src={misLogo} alt="MIS" className="module-sidebar-logo h-11 w-auto shrink-0" />
            <div className="module-sidebar-label min-w-0">
              <p className="truncate text-sm font-semibold leading-5 text-[#0a1e36]">{props.moduleName}</p>
              <p className="truncate text-xs leading-5 text-slate-500">{props.moduleSubtitle}</p>
            </div>
          </div>
          <button aria-label={props.closeNavigationLabel} className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-mis-border text-slate-500 hover:bg-slate-50 lg:hidden" onClick={() => setOpen(false)} type="button">
            <X className="h-5 w-5" />
          </button>
        </div>

        <nav className="module-sidebar-scroll min-h-0 flex-1 overflow-y-auto overscroll-contain px-3 py-3">
          {groups.map((group, index) => {
            const expanded = (collapsed && desktop) || !closedGroups.includes(group.id);
            const groupId = `${navigationId}-group-${index}`;
            return <section className="module-sidebar-section" key={group.id}>
              {group.label ? <button aria-controls={groupId} aria-expanded={expanded} className="module-sidebar-group flex w-full items-center justify-between gap-2 px-3 pb-1.5 pt-2 text-start text-[11px] font-semibold text-slate-500" onClick={() => setClosedGroups(current => current.includes(group.id) ? current.filter(id => id !== group.id) : [...current, group.id])} type="button">
                <span>{group.label}</span><ChevronDown aria-hidden="true" className={`h-3.5 w-3.5 shrink-0 transition-transform ${expanded ? '' : '-rotate-90 rtl:rotate-90'}`} />
              </button> : null}
              <div className="space-y-0.5" hidden={!expanded} id={groupId}>
                {group.items.map(({ to, label, icon: Icon }) => <NavLink aria-label={label} to={to} title={label} onClick={() => setOpen(false)} className={({ isActive }) => linkClass(isActive)} key={to}>
                  {({ isActive }) => <>
                    {isActive ? <span aria-hidden="true" className={`absolute inset-y-2 start-0 w-[3px] rounded-full ${chrome.accent}`} /> : null}
                    <Icon aria-hidden="true" className={`h-[18px] w-[18px] shrink-0 ${isActive ? chrome.icon : 'text-mis-muted group-hover:text-slate-600'}`} />
                    <span className="module-sidebar-label min-w-0 leading-5">{label}</span>
                  </>}
                </NavLink>)}
              </div>
            </section>;
          })}
        </nav>

        <div className="module-sidebar-footer shrink-0 border-t border-mis-border bg-slate-50/90 p-3">
          <NavLink
            to={props.profilePath}
            aria-label={props.profileLabel}
            title={props.profileLabel}
            className={({ isActive }) => `module-sidebar-link mb-1 flex min-h-11 items-center gap-3 rounded-lg px-3 py-2 ${isActive ? chrome.active : 'text-slate-600 hover:bg-white'}`}
          >
            <UserCircle className="h-7 w-7 shrink-0 text-mis-muted" />
            <div className="module-sidebar-label min-w-0">
              <p className="truncate text-sm font-semibold leading-5 text-mis-navy">{props.userName || '—'}</p>
              <p className="truncate text-xs leading-5 text-slate-500">{props.profileLabel}</p>
            </div>
          </NavLink>
          <ModuleSwitcherLink collapsed={collapsed} />
          <div className="module-sidebar-footer-actions mt-1 grid grid-cols-2 gap-1">
            <button
              className="module-sidebar-link flex min-h-10 items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-semibold text-slate-600 hover:bg-white hover:text-mis-primary"
              onClick={props.onLanguageToggle}
              aria-label={props.languageLabel}
              title={props.languageLabel}
              type="button"
            >
              <Languages className="h-5 w-5 shrink-0" />
              <span className="module-sidebar-label min-w-0">{props.languageLabel}</span>
            </button>
            <button
              className="module-sidebar-link flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 py-2 text-sm font-semibold text-slate-600 hover:bg-rose-50 hover:text-rose-700"
              onClick={props.onSignOut}
              aria-label={props.signOutLabel}
              title={props.signOutLabel}
              type="button"
            >
              <LogOut className="h-5 w-5 shrink-0" />
              <span className="module-sidebar-label whitespace-nowrap">{props.signOutLabel}</span>
            </button>
          </div>
        </div>
      </aside>

      <div className="module-content min-w-0">
        <header className="sticky top-0 z-20 flex h-16 min-w-0 items-center border-b border-mis-border bg-white/95 px-4 shadow-[0_1px_0_rgba(15,23,42,.02)] backdrop-blur sm:px-6 xl:px-8">
          <button ref={menuButton} aria-expanded={open} aria-label={props.openNavigationLabel} className="me-3 inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-mis-border bg-white text-mis-navy shadow-sm hover:bg-slate-50 lg:hidden" onClick={() => setOpen(true)} type="button">
            <Menu className="h-5 w-5" />
          </button>
          <div className="min-w-0 flex-1">
            <p className={`truncate text-xs font-semibold leading-5 ${chrome.eyebrow}`}>{props.showCurrentPageTitle ? props.moduleName : props.companyLabel}</p>
            <p className="truncate text-base font-semibold leading-6 text-[#0a1e36]">{props.showCurrentPageTitle ? currentNavigationItem?.label ?? props.headerTitle : props.headerTitle}</p>
          </div>
          {props.headerAside ? <div className="ms-auto flex min-w-0 items-center gap-2">{props.headerAside}</div> : null}
        </header>
        <main className="module-main min-w-0 overflow-x-hidden p-4 sm:p-6 xl:p-8">
          <div className="mx-auto min-w-0 w-full max-w-[1680px]"><Outlet /></div>
        </main>
      </div>
    </div>
  );
}
