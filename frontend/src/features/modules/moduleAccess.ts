import type { AuthenticatedUser } from '../auth/types/auth';

export type ModuleId = 'admin' | 'hr' | 'collections' | 'finance';

export interface AccessibleModule {
  id: ModuleId;
  homePath: string;
}

const moduleOrder: AccessibleModule[] = [
  { id: 'finance', homePath: '/finance/dashboard' },
  { id: 'collections', homePath: '/collections/dashboard' },
  { id: 'hr', homePath: '/hr/dashboard' },
  { id: 'admin', homePath: '/admin/dashboard' },
];

function hasPermission(user: AuthenticatedUser, ...permissions: string[]) {
  return user.permissions.includes('*') || permissions.some(permission => user.permissions.includes(permission));
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
    default:
      return user.department === department;
  }
}
