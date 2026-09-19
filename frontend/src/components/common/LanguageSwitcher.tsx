import { useLocalization } from '../../context/LocalizationContext';

interface LanguageSwitcherProps {
  className?: string;
  tone?: 'light' | 'dark';
}

export function LanguageSwitcher({ className = '', tone = 'light' }: LanguageSwitcherProps) {
  const { language, setLanguage, t } = useLocalization();
  const dark = tone === 'dark';

  return (
    <div
      aria-label={t('language')}
      className={`inline-flex rounded-full border p-0.5 ${dark ? 'border-white/15 bg-white/10' : 'border-mis-border bg-white'} ${className}`}
      role="group"
    >
      <button
        aria-pressed={language === 'ar'}
        className={`min-h-8 rounded-full px-3 text-xs font-semibold transition ${
          language === 'ar'
            ? dark
              ? 'bg-white text-[#0a1e36]'
              : 'bg-[#0a1e36] text-white'
            : dark
              ? 'text-white/75 hover:bg-white/10 hover:text-white'
              : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
        }`}
        lang="ar"
        onClick={() => setLanguage('ar')}
        type="button"
      >
        {t('arabic')}
      </button>
      <button
        aria-pressed={language === 'en'}
        className={`min-h-8 rounded-full px-3 text-xs font-semibold transition ${
          language === 'en'
            ? dark
              ? 'bg-white text-[#0a1e36]'
              : 'bg-[#0a1e36] text-white'
            : dark
              ? 'text-white/75 hover:bg-white/10 hover:text-white'
              : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
        }`}
        lang="en"
        onClick={() => setLanguage('en')}
        type="button"
      >
        {t('english')}
      </button>
    </div>
  );
}
