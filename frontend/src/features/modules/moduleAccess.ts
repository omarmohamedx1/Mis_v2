import type { AuthenticatedUser } from '../auth/types/auth';

export type ModuleId = 'admin' | 'hr' | 'collections' | 'finance' | 'data-entry' | 'legal';

export interface AccessibleModule {
  id: ModuleId;
  homePath: string;
}

const moduleOrder: AccessibleModule[] = [
  { id: 'data-entry', homePath: '/data-entry/dashboard' },
  { id: 'finance', homePath: '/finance/dashboard' },
  { id: 'collections', homePath: '/collections/dashboard' },
  { id: 'legal', homePath: '/legal/dashboard' },
  { id: 'hr', homePath: '/hr/dashboard' },
  { id: 'admin', homePath: '/admin/dashboard' },
];

function hasPermission(user: AuthenticatedUser, ...permissions: string[]) {
  return user.permissions.includes('*') || permissions.some(permission => user.permissions.includes(permission));
}

export function isSystemAdmin(user: AuthenticatedUser | null | undefined) {
  return Boolean(user?.roles.includes('Admin') || user?.permissions.includes('*'));
}

export function hasHrFeature(user: AuthenticatedUser | null | undefined, permissions: string[], roles: string[] = ['HrManager', 'HrOfficer']) {
  if (!user) return false;
  if (user.roles.includes('Admin') || user.permissions.includes('*')) return true;
  return roles.some((role) => user.roles.includes(role)) || hasPermission(user, ...permissions);
}

export function canAccessModule(user: AuthenticatedUser, moduleId: ModuleId) {
  const isAdmin = user.roles.includes('Admin');

  switch (moduleId) {
    case 'admin':
      return isAdmin;
    case 'hr':
      return isAdmin || user.department === 'HR' || hasPermission(user, 'hr.access');
    case 'collections':
      return isAdmin || user.department === 'COLLECTIONS' || hasPermission(user, 'collections.access') || user.roles.some(role => role.startsWith('Collections'));
    case 'finance':
      return isAdmin || user.department === 'ACCOUNTING' || hasPermission(user, 'finance.access', 'accounting.access');
    case 'data-entry':
      return isAdmin || user.department === 'DATA_ENTRY' || hasPermission(user, 'data_entry.access') || user.roles.includes('DataEntry');
    case 'legal':
      return isAdmin || user.department === 'LEGAL' || hasPermission(user, 'legal.access', 'legal.case.manage') || user.roles.includes('LegalOfficer');
  }
}

export function getAccessibleModules(user: AuthenticatedUser) {
  return moduleOrder.filter(module => canAccessModule(user, module.id));
}

export function canAccessDepartment(user: AuthenticatedUser, department: string) {
  if (user.roles.includes('Admin') || user.permissions.includes('*')) return true;

  switch (department) {
    case 'HR':
      return canAccessModule(user, 'hr');
    case 'COLLECTIONS':
      return canAccessModule(user, 'collections');
    case 'ACCOUNTING':
      return canAccessModule(user, 'finance');
    case 'DATA_ENTRY':
      return canAccessModule(user, 'data-entry');
    case 'LEGAL':
      return canAccessModule(user, 'legal');
    default:
      return user.department === department;
  }
}
