import { brandLogoByCode, resolveBrandCode, type BrandCode } from './logoMap';

const brandNames: Record<BrandCode, string> = {
  AAIB: 'Arab African International Bank',
  ABK: 'Al Ahli Bank of Kuwait - Egypt',
  ADIB: 'Abu Dhabi Islamic Bank - Egypt',
  ALEXBANK: 'ALEXBANK',
  AMAN: 'AMAN',
  ATTIJARIWAFA: 'Attijariwafa Bank Egypt',
  BANK_NXT: 'Bank NXT',
  BDC: 'Banque du Caire',
  BM: 'Banque Misr',
  CAE: 'Crédit Agricole Egypt',
  CIB: 'Commercial International Bank',
  ELAB: 'ELAB',
  ENBD: 'Emirates NBD Egypt',
  HSBC: 'HSBC Egypt',
  MNT_HALAN: 'MNT-Halan',
  NBE: 'National Bank of Egypt',
  NBK: 'National Bank of Kuwait - Egypt',
  PREMIUM_CARD: 'Premium Card',
  QIB: 'QIB',
  QNB: 'QNB Egypt',
  RAYA: 'Raya Holding',
};

export function BrandLogo({
  code,
  className = 'brand-logo h-16 w-full max-w-[140px] object-contain',
}: {
  code: BrandCode | string;
  className?: string;
}) {
  const resolved = resolveBrandCode(String(code));
  if (!resolved) return null;

  return (
    <img
      src={brandLogoByCode[resolved]}
      alt={brandNames[resolved]}
      className={className}
      loading="lazy"
      decoding="async"
      style={{ objectFit: 'contain' }}
    />
  );
}
