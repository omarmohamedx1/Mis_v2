import type { ReactNode } from 'react';
import misLogo from '../../assets/mis-logo.svg';
import { useLocalization } from '../../context/LocalizationContext';
import { LanguageSwitcher } from '../common/LanguageSwitcher';

interface AuthLayoutProps {
  children: ReactNode;
}

export function AuthLayout({ children }: AuthLayoutProps) {
  const { t } = useLocalization();

  return (
    <main className="min-h-dvh bg-[#f4f6f9] text-mis-ink md:grid md:grid-cols-[minmax(280px,0.9fr)_minmax(0,1.1fr)]">
      <section className="relative hidden min-h-dvh overflow-hidden text-white md:flex">
        <div className="auth-brand-grid absolute inset-0" aria-hidden="true" />
        <div className="relative z-10 flex w-full flex-col justify-between px-10 py-10 xl:px-14">
          <div>
            <div className="inline-flex items-center rounded-md bg-white px-3 py-2 shadow-sm">
              <img src={misLogo} alt={t('companyLogoAlt')} className="h-14 w-auto" />
            </div>
            <p className="mt-12 text-[11px] font-semibold uppercase tracking-[0.22em] text-sky-200/85">{t('collectionFirm')}</p>
            <h1 className="mt-3 max-w-md text-[2rem] font-semibold leading-snug xl:text-[2.35rem]">{t('collectionSystem')}</h1>
            <p className="mt-4 max-w-md text-sm leading-7 text-slate-300">{t('secureTagline')}</p>
            <ul className="mt-10 max-w-sm space-y-3 text-sm leading-6 text-slate-200">
              <li className="flex gap-3">
                <span className="mt-[0.55rem] h-1.5 w-1.5 shrink-0 rounded-full bg-sky-300" aria-hidden="true" />
                {t('internalSystem')}
              </li>
              <li className="flex gap-3">
                <span className="mt-[0.55rem] h-1.5 w-1.5 shrink-0 rounded-full bg-sky-300" aria-hidden="true" />
                {t('authorizedPersonnel')}
              </li>
              <li className="flex gap-3">
                <span className="mt-[0.55rem] h-1.5 w-1.5 shrink-0 rounded-full bg-sky-300" aria-hidden="true" />
                {t('accessLogged')}
              </li>
            </ul>
          </div>
          <p className="text-xs text-slate-400">© {new Date().getFullYear()} {t('collectionFirm')}</p>
        </div>
      </section>

      <section className="flex min-h-dvh flex-col px-5 py-5 sm:px-10">
        <div className="flex items-center justify-between gap-3 md:justify-end">
          <img src={misLogo} alt={t('companyLogoAlt')} className="h-12 w-auto md:hidden" />
          <LanguageSwitcher />
        </div>
        <div className="flex flex-1 items-center justify-center py-10">
          <div className="w-full max-w-[400px]">{children}</div>
        </div>
      </section>
    </main>
  );
}
