import { Activity, BadgeDollarSign, BarChart3, BriefcaseBusiness, Building2, ClipboardList, Gauge, HandCoins, Landmark, MapPinned, Settings, UserRound, UsersRound, WalletCards } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  { to: '/collections/dashboard', label: 'sidebarOverview', icon: Gauge, group: 'sidebarGroupOverview' },
  { to: '/collections/creditors', label: 'sidebarClients', icon: Building2, group: 'sidebarGroupOverview' },
  { to: '/collections/collectors', label: 'sidebarTeam', icon: UserRound, group: 'sidebarGroupOverview' },
  { to: '/collections/cases', label: 'sidebarCases', icon: BriefcaseBusiness, group: 'sidebarGroupOperations' },
  { to: '/collections/assignments', label: 'sidebarAssignments', icon: UsersRound, supervisorOnly: true, group: 'sidebarGroupOperations' },
  { to: '/collections/promises', label: 'sidebarPromises', icon: HandCoins, group: 'sidebarGroupOperations' },
  { to: '/collections/payments', label: 'sidebarPayments', icon: WalletCards, group: 'sidebarGroupOperations' },
  { to: '/collections/visits', label: 'sidebarVisits', icon: MapPinned, group: 'sidebarGroupOperations' },
  { to: '/banks', label: 'sidebarBanks', icon: Landmark, group: 'sidebarGroupOrganizations' },
  { to: '/installment-companies', label: 'sidebarFinanceCompanies', icon: BadgeDollarSign, group: 'sidebarGroupOrganizations' },
  { to: '/collections/reports', label: 'sidebarReports', icon: BarChart3, group: 'sidebarGroupGovernance' },
  { to: '/collections/data-batches', label: 'sidebarDataImports', icon: ClipboardList, supervisorOnly: true, group: 'sidebarGroupGovernance' },
  { to: '/collections/audit', label: 'sidebarAudit', icon: Activity, auditOnly: true, group: 'sidebarGroupGovernance' },
  { to: '/collections/settings', label: 'sidebarSettings', icon: Settings, operationsOnly: true, group: 'sidebarGroupGovernance' },
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
      groupId: link.group,
    }));
  return <ModuleLayoutShell collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'} companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'} expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'} headerTitle={ct('sidebarOverview')} showCurrentPageTitle isRtl={isRtl} languageLabel={ar ? 'English' : 'العربية'} moduleName={ar ? 'إدارة التحصيل' : 'Collections'} moduleSubtitle={ar ? 'نظام إدارة العمليات' : 'Operations management'} navigation={navigation} navigationLabel={ct('navigation')} onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={ct('openNavigation')} closeNavigationLabel={ct('closeNavigation')} profileLabel={ar ? 'الحساب الشخصي' : 'My profile'} profilePath="/collections/profile" signOutLabel={ct('signOut')} storageKey="mis.collections.sidebar" theme="collections" userName={user?.fullName} />;
}
