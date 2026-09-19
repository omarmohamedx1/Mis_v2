export const masterDataCategories = [
  'departments',
  'positions',
  'branches',
  'employment-types',
  'contract-types',
  'leave-types',
  'document-types',
  'delegation-types',
] as const;

export type MasterDataCategory = (typeof masterDataCategories)[number];

export function isMasterDataCategory(value: string): value is MasterDataCategory {
  return masterDataCategories.includes(value as MasterDataCategory);
}

export interface MasterDataItem {
  id: string;
  category: MasterDataCategory;
  code: string;
  nameEnglish: string;
  nameArabic: string | null;
  description: string | null;
  departmentId: string | null;
  departmentName: string | null;
  address: string | null;
  isActive: boolean;
  defaultAnnualEntitlement: number | null;
  requiresAttachment: boolean | null;
  requiresExpiryDate: boolean | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface MasterDataLookup {
  id: string;
  code: string;
  nameEnglish: string;
  nameArabic: string | null;
  isActive: boolean;
  departmentId?: string | null;
}

export interface PagedMasterData {
  items: MasterDataItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface MasterDataQuery {
  category: MasterDataCategory;
  isActive: boolean | null;
  page: number;
  pageSize: number;
  search: string;
}

export interface SaveMasterDataRequest {
  code: string;
  nameEnglish: string;
  nameArabic: string | null;
  description: string | null;
  departmentId: string | null;
  address: string | null;
  defaultAnnualEntitlement: number | null;
  requiresAttachment: boolean | null;
  requiresExpiryDate: boolean | null;
  isActive: boolean;
}
