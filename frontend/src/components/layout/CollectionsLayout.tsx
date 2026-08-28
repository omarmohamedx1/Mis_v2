import { Activity, BadgeDollarSign, BarChart3, BriefcaseBusiness, Building2, FileUp, Gauge, Landmark, MapPinned, Settings, WalletCards } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useCollectionsLocalization } from '../../features/collections/localization/collectionsTranslations';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  { to: '/collections/dashboard', label: 'overview', icon: Gauge }, { to: '/banks', label: 'banks', icon: Landmark },
  { to: '/collections/installment-companies', label: 'installmentCompanies', icon: BadgeDollarSign }, { to: '/collections/clients', label: 'clients', icon: Building2 },
  { to: '/collections/cases', label: 'cases', icon: BriefcaseBusiness }, { to: '/collections/payments', label: 'payments', icon: WalletCards },
  { to: '/collections/visits', label: 'visits', icon: MapPinned }, { to: '/collections/reports', label: 'reports', icon: BarChart3 },
  { to: '/collections/audit', label: 'audit', icon: Activity, auditOnly: true }, { to: '/collections/imports', label: 'imports', icon: FileUp, operationsOnly: true },
  { to: '/collections/settings', label: 'settings', icon: Settings, operationsOnly: true },
] as const;

export function CollectionsLayout() {
  const { language, setLanguage, isRtl, ct } = useCollectionsLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const navigation: ModuleNavigationItem[] = links
    .filter((link) => (!('auditOnly' in link) || user?.roles.some((role) => ['Admin', 'CollectionsOperationsManager', 'CollectionsAuditor'].includes(role))) && (!('operationsOnly' in link) || user?.roles.some((role) => ['Admin', 'CollectionsOperationsManager'].includes(role))))
    .map(({ to, label, icon }) => ({ to, icon, label: ct(label) }));
  return <ModuleLayoutShell collapseLabel={language === 'ar' ? 'طي القائمة' : 'Collapse navigation'} companyLabel={language === 'ar' ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'} expandLabel={language === 'ar' ? 'توسيع القائمة' : 'Expand navigation'} headerTitle={ct('commandCenter')} isRtl={isRtl} languageLabel={language === 'ar' ? 'English' : 'العربية'} moduleName={ct('collections')} moduleSubtitle={ct('commandCenter')} navigation={navigation} navigationLabel={ct('navigation')} onLanguageToggle={() => setLanguage(language === 'ar' ? 'en' : 'ar')} onSignOut={() => { logout(); navigate('/login', { replace: true }); }} openNavigationLabel={ct('openNavigation')} closeNavigationLabel={ct('closeNavigation')} profileLabel={language === 'ar' ? 'ملفي الشخصي' : 'My profile'} profilePath="/collections/profile" signOutLabel={ct('signOut')} storageKey="mis.collections.sidebar" theme="collections" userName={user?.fullName} />;
}
