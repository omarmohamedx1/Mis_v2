export interface CollectionScoreCell {
  category: string;
  caseCount: number | null;
  actualBalance: number | null;
  targetPercent: number | null;
  targetBalance: number | null;
  missing: number | null;
  percent: number | null;
  weight: number | null;
  actualVsTarget: number | null;
  score: number | null;
}

export interface CollectionScoreBucket {
  code: string;
  version: string;
  categories: CollectionScoreCell[];
}

export interface CollectionScoreOrganization {
  id: string;
  code: string;
  nameArabic: string;
  nameEnglish: string;
  organizationType: string;
  logoUrl?: string | null;
}

export interface CollectionScorePortfolio {
  id: string;
  organizationId: string;
  code: string;
  nameArabic: string;
  nameEnglish: string;
  currencyCode: string;
}

export interface CollectionScoreDashboard {
  organization: CollectionScoreOrganization;
  portfolioType: 'ACT' | 'WO' | 'CORP';
  portfolio: CollectionScorePortfolio;
  reportingPeriod: string;
  canEdit: boolean;
  summary: {
    totalDueAmount: number | null;
    paid: number | null;
    scoreAll: number | null;
  };
  buckets: CollectionScoreBucket[];
}

export interface CollectionScorePeriods {
  currentPeriod: string;
  periods: string[];
}

export interface CollectionScoreAudit {
  id: string;
  portfolioType: string;
  portfolioId: string | null;
  reportingPeriod: string;
  bucket: string;
  category: string;
  beforeJson: string | null;
  afterJson: string;
  modifiedBy: string;
  modifiedAt: string;
}

export type CollectionScoreField =
  | 'caseCount'
  | 'actualBalance'
  | 'targetPercent'
  | 'targetBalance'
  | 'missing'
  | 'percent'
  | 'weight'
  | 'actualVsTarget'
  | 'score';

export interface SaveCollectionScoreBucket {
  reportingPeriod: string;
  expectedVersion: string;
  categories: CollectionScoreCell[];
}
