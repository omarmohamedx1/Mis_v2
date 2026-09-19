import { ArrowUpRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { BankLogo } from './BankLogo';
import type { BankDirectoryItem } from '../types/collections';
import { useCollectionsLocalization } from '../localization/collectionsTranslations';

export function BankDirectoryCard({ bank, basePath = '/banks', openLabel }: { bank: BankDirectoryItem; basePath?: string; openLabel?: string }) {
  const { language, ct } = useCollectionsLocalization();
  const name = language === 'ar' ? bank.nameArabic : bank.nameEnglish;

  return (
    <Link
      aria-label={`${openLabel ?? ct('openBank')}: ${name}`}
      className="group relative flex min-h-48 flex-col overflow-hidden rounded-[1.75rem] border border-mis-border bg-white p-6 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-mis-sky hover:shadow-panel focus-visible:border-mis-blue"
      to={`${basePath}/${bank.id}`}
    >
      <span className="absolute inset-x-0 top-0 h-1 bg-mis-primary opacity-0 transition-opacity group-hover:opacity-100" />
      <div className="flex items-start justify-between gap-5">
        <BankLogo className="h-20 w-20 rounded-3xl" code={bank.code} logoUrl={bank.logoUrl} name={name} />
        <span className="flex h-10 w-10 items-center justify-center rounded-full border border-mis-border text-slate-400 transition group-hover:border-mis-sky group-hover:bg-mis-pale group-hover:text-mis-primary">
          <ArrowUpRight className="h-5 w-5 rtl:-rotate-90" aria-hidden="true" />
        </span>
      </div>
      <div className="mt-auto pt-7">
        <h2 className="text-lg font-bold text-mis-navy">{name}</h2>
        <p className="mt-1 text-xs font-semibold uppercase tracking-[0.18em] text-slate-400" data-bidi="ltr">{bank.code}</p>
      </div>
    </Link>
  );
}
