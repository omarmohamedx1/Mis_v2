import type { BankTargetValues, BankTargetRow } from './types/bankTargets';

export const targetFields = ['moreTargetPercent', 'moreWeight', 'activeClassWeight', 'moreClassWeight', 'wOffBelow90', 'wOffAbove90'] as const;
export type TargetField = typeof targetFields[number];
export type TargetDraft = Record<TargetField, string> & { rows: { bucket: number; movement: BankTargetRow['movement']; targetPercent: string; weight: string }[] };
export function targetDraft(values?: BankTargetValues): TargetDraft {
  const rows = values?.rows ?? [1, 2, 3, 4, 5].flatMap(bucket =>
    (bucket === 1 ? ['FWD', 'NORM'] as const : ['FWD', 'BWD', 'NORM'] as const).map(movement => ({ bucket, movement, targetPercent: '', weight: '' })));
  const field = (key: TargetField) => values ? String(values[key]) : '';
  return { moreTargetPercent: field('moreTargetPercent'), moreWeight: field('moreWeight'), activeClassWeight: field('activeClassWeight'),
    moreClassWeight: field('moreClassWeight'), wOffBelow90: field('wOffBelow90'), wOffAbove90: field('wOffAbove90'),
    rows: rows.map(row => ({ ...row, targetPercent: String(row.targetPercent), weight: String(row.weight) })) };
}
export function validTargetNumber(raw: string): boolean { return /^\d+(\.\d{1,2})?$/.test(raw) && Number(raw) <= 100; }
export function parseTargetDraft(draft: TargetDraft): BankTargetValues | null {
  if (![...targetFields.map(key => draft[key]), ...draft.rows.flatMap(row => [row.targetPercent, row.weight])].every(validTargetNumber)) return null;
  return { moreTargetPercent: Number(draft.moreTargetPercent), moreWeight: Number(draft.moreWeight), activeClassWeight: Number(draft.activeClassWeight),
    moreClassWeight: Number(draft.moreClassWeight), wOffBelow90: Number(draft.wOffBelow90), wOffAbove90: Number(draft.wOffAbove90),
    rows: draft.rows.map(row => ({ ...row, targetPercent: Number(row.targetPercent), weight: Number(row.weight) })) };
}
// Integer hundredths avoid floating-point false failures at the 100.00 boundary.
export function targetTotal(values: string[]): number | null {
  return values.every(validTargetNumber) ? values.reduce((sum, raw) => sum + Math.round(Number(raw) * 100), 0) / 100 : null;
}
export function validTargetPeriod(value: string): boolean {
  return /^\d{4}-(0[1-9]|1[0-2])$/.test(value) && Number(value.slice(0, 4)) >= 1900 && Number(value.slice(0, 4)) <= 9998;
}
