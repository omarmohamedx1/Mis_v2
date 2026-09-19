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

export function organizationPath(kind: OrganizationKind, organizationId?: string) {
  const root = organizationRoot(kind);
  return organizationId ? `${root}/${organizationId}` : root;
}

export function displayPrimary(value: string) {
  return value.toUpperCase() === 'WO' ? 'W.O' : value.toUpperCase();
}

export function deskLabelKey(value: string) {
  const raw = value.toUpperCase().replace(/\./g, '');
  return `desk${raw}`;
}

export function deskHintKey(primary: PrimaryClassification, secondary?: SubClassification) {
  if (primary === 'CORP' && secondary === 'ACT') return 'deskCORPActHint';
  if (primary === 'CORP' && secondary === 'WO') return 'deskCORPWoHint';
  return `desk${(secondary ?? primary).toUpperCase().replace(/\./g, '')}Hint`;
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

export const DESK_SLOTS: OrganizationClassification[] = PRIMARY_OPTIONS.flatMap((primary) =>
  secondaryOptions(primary).map((secondary) => ({ primary, secondary })),
);

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

export function classificationPath(kind: OrganizationKind, organizationId: string, primary: PrimaryClassification, secondary?: SubClassification) {
  const root = organizationPath(kind, organizationId);
  const p = primary.toLowerCase();
  if (!secondary) return `${root}/${p}`;
  return `${root}/${p}/${secondary.toLowerCase()}`;
}

export function organizationWorkspaceBase(kind: OrganizationKind, organizationId: string, classification: OrganizationClassification) {
  return classificationPath(kind, organizationId, classification.primary, classification.secondary);
}

export function classificationQuery(classification: OrganizationClassification) {
  return {
    primaryClassification: classification.primary,
    subClassification: classification.secondary,
  };
}

function pathParts(pathname: string) {
  return pathname.split('/').filter(Boolean);
}

function isOrganizationRoot(value?: string | null) {
  return value === 'banks' || value === 'installment-companies';
}

/** Parse `/banks/{id}/act/loan/...` (and legacy `/banks/act/loan/{id}/...`) from the current path. */
export function classificationFromPathname(pathname: string): OrganizationClassification | null {
  const parts = pathParts(pathname);
  if (parts.length < 4 || !isOrganizationRoot(parts[0])) return null;
  if (isUuid(parts[1])) return parseClassification(parts[2], parts[3]);
  return parseClassification(parts[1], parts[2]);
}

/** Rewrite `/banks/act/loan/{id}/...` to `/banks/{id}/act/loan/...`. */
export function rewriteLegacyOrganizationPath(pathname: string, search = '') {
  const parts = pathParts(pathname);
  if (parts.length < 4 || !isOrganizationRoot(parts[0])) return null;
  const classification = parseClassification(parts[1], parts[2]);
  if (!classification || !isUuid(parts[3])) return null;
  const next = [parts[0], parts[3], classification.primary.toLowerCase(), classification.secondary.toLowerCase(), ...parts.slice(4)];
  return `/${next.join('/')}${search}`;
}
