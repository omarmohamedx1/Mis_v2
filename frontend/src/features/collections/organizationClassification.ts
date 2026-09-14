export type OrganizationKind = 'bank' | 'installment';
export type PrimaryClassification = 'ACT' | 'WO' | 'CORP';
export type ProductClassification = 'LOAN' | 'VISA' | 'AUTO';
export type CorpSubClassification = 'ACT' | 'WO';
export type SubClassification = ProductClassification | CorpSubClassification;

export type OrganizationClassification = {
  primary: PrimaryClassification;
  secondary: SubClassification;
};

const UUID =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export const PRIMARY_OPTIONS: PrimaryClassification[] = ['ACT', 'WO', 'CORP'];
export const PRODUCT_OPTIONS: ProductClassification[] = ['LOAN', 'VISA', 'AUTO'];
export const CORP_SUB_OPTIONS: CorpSubClassification[] = ['ACT', 'WO'];

export function organizationRoot(kind: OrganizationKind) {
  return kind === 'installment' ? '/installment-companies' : '/banks';
}

export function displayPrimary(value: string) {
  return value.toUpperCase() === 'WO' ? 'W.O' : value.toUpperCase();
}

export function normalizePrimary(value?: string | null): PrimaryClassification | null {
  const raw = (value ?? '').trim().toUpperCase().replace(/\./g, '');
  if (raw === 'ACT' || raw === 'WO' || raw === 'CORP') return raw;
  return null;
}

export function normalizeSecondary(primary: PrimaryClassification, value?: string | null): SubClassification | null {
  const raw = (value ?? '').trim().toUpperCase().replace(/\./g, '');
  if (primary === 'CORP') return raw === 'ACT' || raw === 'WO' ? raw : null;
  return raw === 'LOAN' || raw === 'VISA' || raw === 'AUTO' ? raw : null;
}

export function secondaryOptions(primary: PrimaryClassification): SubClassification[] {
  return primary === 'CORP' ? CORP_SUB_OPTIONS : PRODUCT_OPTIONS;
}

export function isUuid(value?: string | null) {
  return Boolean(value && UUID.test(value));
}

export function parseClassification(primary?: string | null, secondary?: string | null): OrganizationClassification | null {
  const p = normalizePrimary(primary);
  if (!p) return null;
  const s = normalizeSecondary(p, secondary);
  if (!s) return null;
  return { primary: p, secondary: s };
}

export function classificationPath(kind: OrganizationKind, primary: PrimaryClassification, secondary?: SubClassification) {
  const root = organizationRoot(kind);
  const p = primary.toLowerCase();
  if (!secondary) return `${root}/${p}`;
  return `${root}/${p}/${secondary.toLowerCase()}`;
}

export function organizationWorkspaceBase(kind: OrganizationKind, classification: OrganizationClassification, organizationId: string) {
  return `${classificationPath(kind, classification.primary, classification.secondary)}/${organizationId}`;
}

export function classificationQuery(classification: OrganizationClassification) {
  return {
    primaryClassification: classification.primary,
    subClassification: classification.secondary,
  };
}

/** Parse `/banks/act/loan/...` or `/installment-companies/corp/wo/...` from the current path. */
export function classificationFromPathname(pathname: string): OrganizationClassification | null {
  const parts = pathname.split('/').filter(Boolean);
  if (parts.length < 3) return null;
  if (parts[0] !== 'banks' && parts[0] !== 'installment-companies') return null;
  return parseClassification(parts[1], parts[2]);
}
