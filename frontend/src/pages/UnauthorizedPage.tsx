import { LogOut, ShieldX } from 'lucide-react';
import { LanguageSwitcher } from '../components/common/LanguageSwitcher';
import { useAuth } from '../context/AuthContext';
import { useLocalization } from '../context/LocalizationContext';

export function UnauthorizedPage() {
  const { logout } = useAuth();
  const { t } = useLocalization();
  return (
    <main className="flex min-h-dvh flex-col bg-[#f4f6f9]">
      <div className="flex justify-end px-5 py-5 sm:px-10">
        <LanguageSwitcher />
      </div>
      <section className="flex flex-1 items-center justify-center px-5 pb-16">
        <div className="w-full max-w-md text-center">
          <ShieldX className="mx-auto h-10 w-10 text-[#0a1e36]" aria-hidden="true" />
          <h1 className="mt-5 text-2xl font-semibold text-[#0a1e36]">{t('accessUnavailable')}</h1>
          <p className="mt-3 text-sm leading-6 text-slate-500">{t('accessHelp')}</p>
          <button
            className="mt-6 inline-flex items-center gap-2 rounded-lg bg-[#0a1e36] px-4 py-3 text-sm font-semibold text-white hover:bg-[#071627]"
            onClick={logout}
            type="button"
          >
            <LogOut className="h-4 w-4" aria-hidden="true" /> {t('signOut')}
          </button>
        </div>
      </section>
    </main>
  );
}
