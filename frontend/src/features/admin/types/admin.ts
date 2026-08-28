export interface AdminRole { id: string; name: string; description?: string; isSystemRole: boolean }
export interface AdminAccessGrant { id: string; permissionCode: string; scopeType: string; clientOrganizationId?: string; clientOrganizationNameAr?: string; clientOrganizationNameEn?: string; status: string; requestedAt: string; grantedAt?: string; expiresAt?: string }
export interface AdminUser { id: string; loginCode: string; username: string; email: string; fullName: string; departmentId: string; departmentCode: string; departmentNameAr: string; departmentNameEn: string; isActive: boolean; createdAt: string; lastLoginAt?: string; roles: AdminRole[]; accessGrants: AdminAccessGrant[] }
export interface AdminUserList { items: AdminUser[]; total: number; page: number; pageSize: number }
export interface AdminDecisionItem { type: string; severity: string; count: number; titleAr: string; titleEn: string; descriptionAr: string; descriptionEn: string }
export interface AdminDepartmentSummary { id: string; code: string; nameAr: string; nameEn: string; totalUsers: number; activeUsers: number; privilegedUsers: number }
export interface AdminAuditItem { id: string; actorUserId: string; actorName: string; action: string; targetType: string; targetId?: string; targetName?: string; details: string; occurredAt: string; sourceIp?: string }
export interface AdminDashboard { totalUsers: number; activeUsers: number; inactiveUsers: number; pendingAccessRequests: number; privilegedUsers: number; expiringAccessCount: number; neverLoggedInCount: number; decisionQueue: AdminDecisionItem[]; departments: AdminDepartmentSummary[]; recentActivity: AdminAuditItem[] }
export interface AdminDepartment { id: string; code: string; nameAr: string; nameEn: string }
export interface AdminClient { id: string; code: string; nameAr: string; nameEn: string; type: string; isActive: boolean }
export interface AdminPermission { code: string; group: string; nameAr: string; nameEn: string; descriptionAr: string; descriptionEn: string; riskLevel: 'LOW'|'MEDIUM'|'HIGH'|'CRITICAL'; allowedScopes: string[] }
export interface AdminReferenceData { departments: AdminDepartment[]; roles: AdminRole[]; clients: AdminClient[]; permissions: AdminPermission[] }
export interface AdminAuditPage { items: AdminAuditItem[]; total: number; page: number; pageSize: number }
export interface SaveAccessGrant { permissionCode: string; scopeType: string; clientOrganizationId?: string; expiresAt?: string }
export interface SaveUserAccess { roleIds: string[]; grants: SaveAccessGrant[]; confirmationPhrase: string }
export interface CreateAdminUser { fullName: string; username: string; email: string; departmentId: string; temporaryPassword: string; roleIds: string[] }
