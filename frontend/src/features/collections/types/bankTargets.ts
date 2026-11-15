export interface BankTargetRow { bucket: number; movement: 'FWD' | 'BWD' | 'NORM'; targetPercent: number; weight: number }
export interface BankTargetValues {
  rows: BankTargetRow[]; moreTargetPercent: number; moreWeight: number; activeClassWeight: number;
  moreClassWeight: number; wOffBelow90: number; wOffAbove90: number;
}
export interface BankTargetConfiguration {
  id: string; status: 'DRAFT' | 'REVIEWED' | 'APPROVED'; version: string; values: BankTargetValues;
  createdBy: string; createdAt: string; updatedBy: string; updatedAt: string; reviewedBy: string | null;
  reviewedAt: string | null; approvedBy: string | null; approvedAt: string | null; copiedFromId: string | null;
}
export interface BankTargetAudit { id: string; action: string; status: string; actorId: string; occurredAt: string; version: string; values: BankTargetValues }
export interface BankTargets {
  configuration: BankTargetConfiguration | null; canEdit: boolean; canApprove: boolean;
  scores: { active: number | null; more: number | null; status: string; issues: string[] }; history: BankTargetAudit[];
}
export interface BankTargetScope { organizationId: string; portfolioType: 'ACT' | 'WO'; portfolioId: string; period: string }
