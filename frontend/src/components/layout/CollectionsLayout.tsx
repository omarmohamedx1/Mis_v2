import { Activity, BadgeDollarSign, BarChart3, BriefcaseBusiness, Building2, ClipboardList, Gauge, HandCoins, Landmark, MapPinned, Settings, UserRound, UsersRound, WalletCards } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  { to: '/collections/dashboard', label: 'overview', icon: Gauge, group: 'navGroupToday' },
  { to: '/collections/creditors', label: 'creditorDashboard', icon: Building2, group: 'navGroupToday' },
  { to: '/collections/collectors', label: 'collectorDashboard', icon: UserRound, group: 'navGroupToday' },
  { to: '/banks', label: 'banks', icon: Landmark, group: 'navGroupWorkspaces' },
  { to: '/installment-companies', label: 'installmentCompanies', icon: BadgeDollarSign, group: 'navGroupWorkspaces' },
  { to: '/collections/cases', label: 'cases', icon: BriefcaseBusiness, group: 'navGroupFloor' },
  { to: '/collections/promises', label: 'promises', icon: HandCoins, group: 'navGroupFloor' },
  { to: '/collections/payments', label: 'payments', icon: WalletCards, group: 'navGroupFloor' },
  { to: '/collections/assignments', label: 'assignments', icon: UsersRound, supervisorOnly: true, group: 'navGroupFloor' },
  { to: '/collections/visits', label: 'visits', icon: MapPinned, group: 'navGroupFloor' },
  { to: '/collections/reports', label: 'reports', icon: BarChart3, group: 'navGroupInsights' },
  { to: '/collections/data-batches', label: 'dataBatches', icon: ClipboardList, supervisorOnly: true, group: 'navGroupInsights' },
  { to: '/collections/audit', label: 'audit', icon: Activity, auditOnly: true, group: 'navGroupInsights' },
  { to: '/collections/settings', label: 'settings', icon: Settings, operationsOnly: true, group: 'navGroupSetup' },
] as const;

export function CollectionsLayout() {
  const { language, setLanguage, isRtl, ct } = useCollectionsLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const ar = language === 'ar';
  const navigation: ModuleNavigationItem[] = links
    .filter((link) =>
      (!('auditOnly' in link) || user?.roles.some((role) => ['Admin', 'CollectionsOperationsManager', 'CollectionsAuditor'].includes(role)))
      && (!('operationsOnly' in link) || user?.roles.some((role) => ['Admin', 'CollectionsOperationsManager'].includes(role)))
      && (!('supervisorOnly' in link) || user?.roles.some((role) => ['Admin', 'CollectionsSupervisor', 'CollectionsOperationsManager'].includes(role))))
    .map((link) => ({
      to: link.to,
      icon: link.icon,
      label: ct(link.label),
      group: ct(link.group),
    }));
  return <ModuleLayoutShell collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'} companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'} expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'} headerTitle={ct('commandCenter')} isRtl={isRtl} languageLabel={ar ? 'English' : 'العربية'} moduleName={ct('collections')} moduleSubtitle={ct('commandCenter')} navigation={navigation} navigationLabel={ct('navigation')} onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={ct('openNavigation')} closeNavigationLabel={ct('closeNavigation')} profileLabel={ar ? 'ملفي الشخصي' : 'My profile'} profilePath="/collections/profile" signOutLabel={ct('signOut')} storageKey="mis.collections.sidebar" theme="collections" userName={user?.fullName} />;
}
