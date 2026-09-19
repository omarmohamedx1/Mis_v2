export const brandLogoByCode = {
  AAIB: '/logos/AAIB.png',
  ABK: '/logos/ABK.png',
  ADIB: '/logos/ADIB.png',
  ALEXBANK: '/logos/ALEXBANK.png',
  AMAN: '/logos/AMAN.png',
  ATTIJARIWAFA: '/logos/ATTIJARIWAFA.png',
  BANK_NXT: '/logos/BANK_NXT.png',
  BDC: '/logos/BDC.png',
  BM: '/logos/BM.png',
  CAE: '/logos/CAE.png',
  CIB: '/logos/CIB.png',
  ELAB: '/logos/ELAB.png',
  ENBD: '/logos/ENBD.png',
  HSBC: '/logos/HSBC.png',
  MNT_HALAN: '/logos/MNT_HALAN.png',
  NBE: '/logos/NBE.png',
  NBK: '/logos/NBK.png',
  PREMIUM_CARD: '/logos/PREMIUM_CARD.png',
  QIB: '/logos/QIB.png',
  QNB: '/logos/QNB.png',
  RAYA: '/logos/RAYA.png',
} as const;

export type BrandCode = keyof typeof brandLogoByCode;

export function resolveBrandCode(code: string): BrandCode | undefined {
  const key = (code || '').trim().toUpperCase().replace(/-/g, '_');
  if (key in brandLogoByCode) return key as BrandCode;
  return undefined;
}
