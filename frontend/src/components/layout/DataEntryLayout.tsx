import { FileUp, History, LayoutDashboard, Paperclip, UsersRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { ModuleLayoutShell, type ModuleNavigationItem } from './ModuleLayoutShell';

const links = [
  ['/data-entry/dashboard', 'لوحة التحكم', 'Dashboard', LayoutDashboard],
  ['/data-entry/clients', 'العملاء', 'Clients', UsersRound],
  ['/data-entry/import', 'رفع العملاء', 'Upload clients', FileUp],
  ['/data-entry/files', 'ملفات العملاء', 'Client files', Paperclip],
  ['/data-entry/history', 'سجل الرفع', 'Upload History', History],
] as const;

export function DataEntryLayout() {
  const { language, isRtl, setLanguage } = useLocalization();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const ar = language === 'ar';
  const navigation: ModuleNavigationItem[] = links.map(([to, arabic, english, icon]) => ({
    to,
    icon,
    label: ar ? arabic : english,
  }));

  return (
    <ModuleLayoutShell
      collapseLabel={ar ? 'طي القائمة' : 'Collapse navigation'}
      companyLabel={ar ? 'شركة MIS للتحصيل' : 'MIS Collection Firm'}
      expandLabel={ar ? 'توسيع القائمة' : 'Expand navigation'}
      headerTitle={ar ? 'إدخال البيانات' : 'Data Entry'}
      isRtl={isRtl}
      languageLabel={ar ? 'English' : 'العربية'}
      moduleName={ar ? 'إدخال البيانات' : 'MIS Data Entry'}
      moduleSubtitle={ar ? 'ملف العملاء منفصل عن مستندات كل عميل' : 'The client file is separate from each client’s papers'}
      navigation={navigation}
      navigationLabel={ar ? 'قائمة إدخال البيانات' : 'Data entry navigation'}
      onLanguageToggle={() => setLanguage(ar ? 'en' : 'ar')}
      onSignOut={() => {
        logout();
        navigate('/login', { replace: true });
      }}
      openNavigationLabel={ar ? 'فتح القائمة' : 'Open navigation'}
      closeNavigationLabel={ar ? 'إغلاق القائمة' : 'Close navigation'}
      profileLabel={ar ? 'ملفي الشخصي' : 'My profile'}
      profilePath="/data-entry/profile"
      signOutLabel={ar ? 'تسجيل الخروج' : 'Sign out'}
      storageKey="mis.data-entry.sidebar"
      theme="collections"
      userName={user?.fullName}
    />
  );
}
