import { Building2 } from 'lucide-react';
import { brandLogoByCode, resolveBrandCode } from '../../../branding/logoMap';
import { env } from '../../../config/env';

interface BankLogoProps { code: string; name: string; logoUrl?: string; className?: string }
const palettes = ['from-sky-600 to-blue-900', 'from-emerald-500 to-teal-800', 'from-violet-500 to-indigo-900', 'from-amber-500 to-orange-800', 'from-rose-500 to-red-900'];
const brandPalettes: Record<string, string> = {
  ALEXBANK: 'from-rose-600 to-red-900',
  ATTIJARIWAFA: 'from-orange-500 to-amber-800',
  CAE: 'from-emerald-600 to-green-900',
  QIB: 'from-teal-600 to-cyan-900',
  BDC: 'from-blue-700 to-indigo-950',
  ELAB: 'from-slate-600 to-slate-900',
  RAYA: 'from-red-600 to-rose-900',
  AMAN: 'from-amber-500 to-orange-700',
  MNT_HALAN: 'from-orange-500 to-red-800',
  'MNT-HALAN': 'from-orange-500 to-red-800',
  PREMIUM_CARD: 'from-yellow-600 to-amber-900',
};

function absoluteLogoUrl(value: string) {
  if (/^https?:\/\//i.test(value)) return value;
  return `${env.apiUrl.replace(/\/api\/?$/, '')}${value.startsWith('/') ? value : `/${value}`}`;
}

export function BankLogo({ code, name, logoUrl, className = 'h-16 w-16' }: BankLogoProps) {
  const key = (code || '').trim().toUpperCase();
  const localCode = resolveBrandCode(key);
  const localSrc = localCode ? brandLogoByCode[localCode] : undefined;
  const src = logoUrl ? absoluteLogoUrl(logoUrl) : localSrc;

  if (src) {
    return (
      <span className={`grid shrink-0 place-items-center overflow-hidden rounded-2xl border border-slate-200 bg-white p-2 shadow-sm ${className}`}>
        <img src={src} alt={`${name} logo`} className="h-full w-full object-contain" loading="lazy" decoding="async" />
      </span>
    );
  }

  const palette = brandPalettes[key] ?? palettes[Array.from(code).reduce((sum, char) => sum + char.charCodeAt(0), 0) % palettes.length];
  const initials = code.replace(/[^A-Z0-9]/gi, '').slice(0, 3).toUpperCase();
  return (
    <span title={name} className={`relative grid shrink-0 place-items-center overflow-hidden rounded-2xl bg-gradient-to-br text-white shadow-sm ${palette} ${className}`}>
      <Building2 className="absolute h-10 w-10 opacity-15" />
      <strong className="relative text-sm tracking-wider">{initials || 'BANK'}</strong>
    </span>
  );
}
