import { AlertOctagon, Check, Copy, KeyRound, LockKeyhole, Plus, Search, ShieldCheck, SlidersHorizontal, Trash2, UserCheck, UserX } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { Button } from '../../components/common/Button';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { ErrorState } from '../../components/common/ErrorState';
import { LoadingSpinner } from '../../components/common/LoadingSpinner';
import { Modal } from '../../components/common/Modal';
import { PageHeader } from '../../components/common/PageHeader';
import { useToast } from '../../components/common/Toast';
import { DateControl } from '../../components/forms/DateControl';
import { ProfessionalSelect } from '../../components/forms/ProfessionalSelect';
import { useLocalization } from '../../context/LocalizationContext';
import { DEPARTMENT_ROLE_HINT, GROUP_ORDER, PERMISSION_GROUPS, impliedPermissionCodes, roleLabel, roleSummary, suggestUsername } from '../../features/admin/roleCatalog';
import { adminService } from '../../features/admin/services/adminService';
import type { AdminClient, AdminCredentialIssue, AdminLinkableEmployee, AdminPermission, AdminReferenceData, AdminUser, SaveAccessGrant } from '../../features/admin/types/admin';
import { getApiErrorMessage } from '../../services/apiClient';

const copy = {
  ar: {
    eyebrow: 'الهوية والوصول', title: 'المستخدمون والصلاحيات', desc: 'أنشئ الحساب، سلّم كلمة مرور لمرة واحدة، ثم خصص الدور والصلاحيات بنطاق واضح.',
    add: 'إضافة مستخدم', search: 'ابحث بالاسم أو الكود أو البريد', allDept: 'كل الأقسام', allStatus: 'كل الحالات',
    employee: 'الموظف', noEmployee: 'بدون ربط موظف', employeeHelp: 'اربط الحساب بموظف من شؤون الموظفين. قسم الأوفيس مستبعد تمامًا.', unlink: 'إلغاء الربط',
    employeeLinked: 'مربوط بالموظف',
    active: 'مفعّل', inactive: 'غير مفعّل', user: 'المستخدم', department: 'القسم', access: 'الوصول الحالي', lastLogin: 'آخر دخول',
    actions: 'الإجراء', never: 'لم يدخل', permissions: 'صلاحية إضافية', review: 'تخصيص الصلاحيات', security: 'الحساب والأمان',
    noUsers: 'لا يوجد مستخدمون مطابقون.', error: 'تعذر تحميل المستخدمين.', retry: 'إعادة المحاولة', pending: 'مقترحة',
    tempBadge: 'كلمة مرور مؤقتة', noAccess: 'بدون صلاحيات بعد',
    accessTitle: 'تخصيص الوصول', accessDesc: 'ابدأ بالدور الوظيفي، ثم أضف فقط ما يحتاجه العمل فوق الدور.',
    roleStep: '١. الدور الوظيفي', extraStep: '٢. صلاحيات إضافية حسب الحاجة',
    roleHint: 'الدور يعطي حزمة جاهزة. الصلاحيات الإضافية للتخصيص الدقيق.',
    extraHint: 'لن تحتاج تكرار ما هو مضمّن في الدور. أضف هنا ما يزيد عن الحزمة.',
    included: 'مضمّنة في الدور', searchPermissions: 'ابحث داخل الصلاحيات', allModules: 'كل الموديولات',
    scope: 'النطاق', own: 'بياناته فقط', team: 'فريقه', dept: 'قسمه', client: 'بنوك محددة', all: 'كل بيانات الموديول',
    expires: 'تاريخ الانتهاء (اختياري)', clients: 'البنوك والشركات المسموحة',
    collectionsHelp: 'نطاق واحد وبنوك واحدة لكل صلاحيات التحصيل المحددة. لا تكرر الاختيار لكل صلاحية.',
    impact: 'ملخص القرار',
    criticalWarning: 'الوصول المعتمد يصل إلى بيانات تشغيلية حقيقية داخل النطاق المحدد، ويُسجَّل باسمك.',
    adminRoleWarning: 'دور مدير النظام يفتح كل شيء. لا تستخدمه لحساب تشغيلي عادي.',
    selected: 'صلاحية إضافية', critical: 'عالية أو حرجة', rolesCount: 'أدوار',
    adminPhrase: 'لتأكيد دور مدير النظام اكتب: GRANT ADMIN ACCESS',
    save: 'اعتماد الوصول', cancel: 'إلغاء', success: 'تم حفظ الصلاحيات وتسجيل القرار.',
    low: 'منخفضة', medium: 'متوسطة', high: 'عالية', criticalRisk: 'حرجة', planned: 'قيد التجهيز',
    statusTitle: 'الحساب والأمان', generate: 'إنشاء كلمة مرور مؤقتة',
    passwordOnce: 'ستظهر مرة واحدة. سلّمها للموظف وسيُجبر على تغييرها عند أول دخول.',
    issuedHelp: 'انسخها الآن. بعد إغلاق هذه الشاشة لن تظهر مرة أخرى.',
    copy: 'نسخ', copied: 'تم النسخ', loginCode: 'كود الدخول', username: 'اسم الدخول', password: 'كلمة المرور المؤقتة',
    statusWarning: 'إيقاف الحساب يمنع الدخول فورًا. التفعيل لا يمنح صلاحيات لم تُراجع.',
    activate: 'تفعيل الحساب', suspend: 'إيقاف الحساب', statusSuccess: 'تم تحديث حالة الحساب.',
    passwordSuccess: 'تم إنشاء كلمة مرور مؤقتة. سيُطلب تغييرها عند الدخول.',
    deleteUser: 'حذف الحساب', deleteUserConfirm: 'هل تريد حذف حساب {name} نهائيًا؟ الحذف متاح للحسابات غير المستخدمة. إذا وُجد سجل تشغيلي استخدم إيقاف الحساب.', deleteUserSuccess: 'تم حذف الحساب.',
    createTitle: 'إضافة مستخدم', createLead: 'النظام ينشئ كلمة مرور مميزة لمرة واحدة. لا حاجة لاختراعها يدويًا.',
    fullName: 'الاسم الكامل', email: 'البريد الإلكتروني', startingRole: 'دور البداية (اختياري)', noRole: 'بدون دور الآن — سأخصص لاحقًا',
    create: 'إنشاء الحساب', createSuccess: 'تم إنشاء الحساب.', nextAccess: 'تخصيص الصلاحيات الآن', done: 'تم',
    issuedTitle: 'سلّم بيانات الدخول',
  },
  en: {
    eyebrow: 'Identity & access', title: 'Users and access', desc: 'Create the account, hand over a one-time password, then assign a role and only the extra access the job needs.',
    add: 'Add user', search: 'Search name, code, or email', allDept: 'All departments', allStatus: 'All statuses',
    employee: 'Employee', noEmployee: 'No employee linked', employeeHelp: 'Link the account to an HR employee. Office staff are excluded.', unlink: 'Unlink',
    employeeLinked: 'Linked employee',
    active: 'Active', inactive: 'Inactive', user: 'User', department: 'Department', access: 'Current access', lastLogin: 'Last sign-in',
    actions: 'Action', never: 'Never', permissions: 'extra permission', review: 'Assign access', security: 'Account & security',
    noUsers: 'No matching users.', error: 'Could not load users.', retry: 'Try again', pending: 'Proposed',
    tempBadge: 'Temporary password', noAccess: 'No access yet',
    accessTitle: 'Assign access', accessDesc: 'Start with the job role, then add only what the work needs on top.',
    roleStep: '1. Job role', extraStep: '2. Extra permissions if needed',
    roleHint: 'A role is a ready pack. Extra permissions are for precise customization.',
    extraHint: 'Do not repeat what the role already includes. Add only what goes beyond the pack.',
    included: 'Included in role', searchPermissions: 'Search permissions', allModules: 'All modules',
    scope: 'Scope', own: 'Own records only', team: 'Their team', dept: 'Their department', client: 'Specific banks', all: 'All module data',
    expires: 'Expiry date (optional)', clients: 'Allowed banks & companies',
    collectionsHelp: 'One scope and one bank list apply to every selected collections permission.',
    impact: 'Decision summary',
    criticalWarning: 'Authorized access reaches live operational data in the selected scope, and is recorded under your name.',
    adminRoleWarning: 'System administrator opens everything. Never use it for a normal operational account.',
    selected: 'extra permissions', critical: 'high or critical', rolesCount: 'roles',
    adminPhrase: 'To confirm System administrator, type: GRANT ADMIN ACCESS',
    save: 'Authorize access', cancel: 'Cancel', success: 'Access saved and the decision was recorded.',
    low: 'Low', medium: 'Medium', high: 'High', criticalRisk: 'Critical', planned: 'Planned',
    statusTitle: 'Account & security', generate: 'Issue a temporary password',
    passwordOnce: 'It is shown once. Hand it to the employee; they must change it at first sign-in.',
    issuedHelp: 'Copy it now. It will not be shown again after this screen closes.',
    copy: 'Copy', copied: 'Copied', loginCode: 'Login code', username: 'Username', password: 'Temporary password',
    statusWarning: 'Suspension blocks sign-in immediately. Activation does not grant unreviewed access.',
    activate: 'Activate account', suspend: 'Suspend account', statusSuccess: 'Account status updated.',
    passwordSuccess: 'A temporary password was issued. It must be changed at next sign-in.',
    deleteUser: 'Delete account', deleteUserConfirm: 'Permanently delete {name}? Allowed only for unused accounts. If the account has operational history, suspend it instead.', deleteUserSuccess: 'Account deleted.',
    createTitle: 'Add user', createLead: 'The system generates a distinctive one-time password. You do not invent it by hand.',
    fullName: 'Full name', email: 'Email', startingRole: 'Starting role (optional)', noRole: 'No role yet — I will assign it next',
    create: 'Create account', createSuccess: 'Account created.', nextAccess: 'Assign access now', done: 'Done',
    issuedTitle: 'Hand over sign-in details',
  },
} as const;

export function AdminUsersPage() {
  const { language } = useLocalization();
  const t = copy[language];
  const toast = useToast();
  const [refs, setRefs] = useState<AdminReferenceData>();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [search, setSearch] = useState('');
  const [department, setDepartment] = useState('');
  const [status, setStatus] = useState('');
  const [selected, setSelected] = useState<AdminUser>();
  const [issued, setIssued] = useState<AdminCredentialIssue>();
  const [mode, setMode] = useState<'access' | 'security' | 'create' | 'issued'>();
  const [deleteTarget, setDeleteTarget] = useState<AdminUser>();
  const [deleting, setDeleting] = useState(false);

  const load = async () => {
    setLoading(true);
    setFailed(false);
    try {
      const [reference, list] = await Promise.all([
        refs ? Promise.resolve(refs) : adminService.referenceData(),
        adminService.users({ search: search || undefined, department: department || undefined, status: status || undefined, pageSize: 100 }),
      ]);
      setRefs(reference);
      setUsers(list.items);
      setTotal(list.total);
    } catch {
      setFailed(true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const id = window.setTimeout(load, 250);
    return () => window.clearTimeout(id);
  }, [search, department, status]);

  const refreshUser = (user: AdminUser) => {
    setUsers((current) => current.map((item) => (item.id === user.id ? user : item)));
    setSelected(user);
  };

  async function deleteUser() {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await adminService.deleteUser(deleteTarget.id);
      setUsers((current) => current.filter((item) => item.id !== deleteTarget.id));
      setTotal((current) => Math.max(0, current - 1));
      if (selected?.id === deleteTarget.id) setSelected(undefined);
      setDeleteTarget(undefined);
      toast.success(t.deleteUserSuccess);
    } catch (error) {
      toast.error(getApiErrorMessage(error, language === 'ar' ? 'تعذر حذف الحساب.' : 'Could not delete the account.'));
    } finally {
      setDeleting(false);
    }
  }

  if (failed && !users.length) return <ErrorState title={t.error} onRetry={load} retryLabel={t.retry} />;

  return (
    <div className="mx-auto max-w-[1600px]">
      <PageHeader eyebrow={t.eyebrow} title={t.title} description={t.desc} actions={<Button fullWidth={false} leftIcon={<Plus className="h-4 w-4" />} onClick={() => setMode('create')} size="md">{t.add}</Button>} />
      <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-col gap-3 border-b border-slate-200 p-4 lg:flex-row">
          <label className="relative flex-1">
            <Search className="absolute start-3 top-3 h-5 w-5 text-slate-400" />
            <input className="field !py-2.5 ps-10" onChange={(event) => setSearch(event.target.value)} placeholder={t.search} value={search} />
          </label>
          <ProfessionalSelect className="field !w-auto !py-2.5" onChange={(event) => setDepartment(event.target.value)} value={department}>
            <option value="">{t.allDept}</option>
            {refs?.departments.map((item) => <option key={item.id} value={item.code}>{language === 'ar' ? item.nameAr : item.nameEn}</option>)}
          </ProfessionalSelect>
          <ProfessionalSelect className="field !w-auto !py-2.5" onChange={(event) => setStatus(event.target.value)} value={status}>
            <option value="">{t.allStatus}</option>
            <option value="ACTIVE">{t.active}</option>
            <option value="INACTIVE">{t.inactive}</option>
          </ProfessionalSelect>
        </div>
        {loading && !users.length ? (
          <div className="flex min-h-72 items-center justify-center"><LoadingSpinner /></div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1080px] text-sm">
              <thead className="bg-slate-50 text-slate-500">
                <tr>
                  <th className="px-5 py-3 text-start">{t.user}</th>
                  <th className="px-5 py-3 text-start">{t.department}</th>
                  <th className="px-5 py-3 text-start">{t.access}</th>
                  <th className="px-5 py-3 text-start">{t.lastLogin}</th>
                  <th className="px-5 py-3 text-start">{t.actions}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {users.map((user) => (
                  <tr className="hover:bg-slate-50/70" key={user.id}>
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-3">
                        <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-100 font-black text-[#0b3b58]">{user.fullName.slice(0, 1).toUpperCase()}</span>
                        <div>
                          <p className="font-bold text-slate-900">{user.fullName}</p>
                          <p className="mt-0.5 text-xs text-slate-500" dir="ltr">{user.loginCode} · {user.username}</p>
                          {user.employeeNumber && <p className="mt-0.5 text-xs font-semibold text-cyan-800">{user.employeeNumber} · {user.employeeName}</p>}
                        </div>
                        <span className={`ms-2 rounded-full px-2 py-1 text-[11px] font-bold ${user.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-600'}`}>{user.isActive ? t.active : t.inactive}</span>
                        {user.mustChangePassword && <span className="rounded-full bg-amber-50 px-2 py-1 text-[11px] font-bold text-amber-800">{t.tempBadge}</span>}
                      </div>
                    </td>
                    <td className="px-5 py-4 font-semibold text-slate-700">{language === 'ar' ? user.departmentNameAr : user.departmentNameEn}</td>
                    <td className="px-5 py-4">
                      <div className="flex flex-wrap gap-1.5">
                        {user.roles.slice(0, 2).map((role) => <span className="rounded-full bg-cyan-50 px-2 py-1 text-xs font-bold text-cyan-800" key={role.id}>{roleLabel(role.name, language)}</span>)}
                        {user.accessGrants.length > 0 && <span className="rounded-full bg-indigo-50 px-2 py-1 text-xs font-bold text-indigo-700">{user.accessGrants.length} {t.permissions}</span>}
                        {!user.roles.length && !user.accessGrants.length && <span className="text-xs text-slate-400">{t.noAccess}</span>}
                        {user.accessGrants.some((grant) => grant.status === 'PENDING') && <span className="rounded-full bg-amber-50 px-2 py-1 text-xs font-bold text-amber-700">{t.pending}</span>}
                      </div>
                    </td>
                    <td className="px-5 py-4 text-slate-600">{user.lastLoginAt ? new Intl.DateTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', { dateStyle: 'medium' }).format(new Date(user.lastLoginAt)) : t.never}</td>
                    <td className="px-5 py-4">
                      <div className="flex gap-2">
                        <button className="inline-flex items-center gap-1.5 rounded-lg bg-[#09263d] px-3 py-2 text-xs font-bold text-white hover:bg-[#164867]" onClick={() => { setSelected(user); setMode('access'); }} type="button">
                          <SlidersHorizontal className="h-3.5 w-3.5" />{t.review}
                        </button>
                        <button className="inline-flex items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50" onClick={() => { setSelected(user); setMode('security'); }} type="button">
                          <KeyRound className="h-3.5 w-3.5" />{t.security}
                        </button>
                        <button className="inline-flex items-center gap-1.5 rounded-lg border border-rose-200 px-3 py-2 text-xs font-bold text-rose-700 hover:bg-rose-50" onClick={() => setDeleteTarget(user)} type="button">
                          <Trash2 className="h-3.5 w-3.5" />{t.deleteUser}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!users.length && <p className="py-16 text-center text-sm text-slate-500">{t.noUsers}</p>}
          </div>
        )}
      <div className="border-t border-slate-200 px-5 py-3 text-xs font-semibold text-slate-500">{total} {t.user}</div>
    </section>
      {selected && refs && (
        <AccessReviewModal
          language={language}
          onClose={() => setMode(undefined)}
          onSaved={(user) => { refreshUser(user); setMode(undefined); toast.success(t.success); }}
          open={mode === 'access'}
          refs={refs}
          user={selected}
        />
      )}
      {selected && (
        <SecurityModal
          language={language}
          onClose={() => setMode(undefined)}
          onDelete={() => { setDeleteTarget(selected); setMode(undefined); }}
          onIssued={(issue) => { refreshUser(issue.user); setIssued(issue); setMode('issued'); }}
          onSaved={(user) => { refreshUser(user); toast.success(t.statusSuccess); }}
          open={mode === 'security'}
          user={selected}
        />
      )}
      {refs && (
        <CreateUserModal
          language={language}
          onClose={() => setMode(undefined)}
          onCreated={(issue) => {
            setUsers((current) => [issue.user, ...current.filter((item) => item.id !== issue.user.id)]);
            setTotal((current) => current + 1);
            setSelected(issue.user);
            setIssued(issue);
            setMode('issued');
          }}
          open={mode === 'create'}
          refs={refs}
        />
      )}
      {issued && (
        <IssuedCredentialsModal
          issue={issued}
          language={language}
          onAssign={() => { setSelected(issued.user); setMode('access'); }}
          onClose={() => { setIssued(undefined); if (mode === 'issued') setMode(undefined); }}
          open={mode === 'issued'}
        />
      )}
      <ConfirmDialog
        confirmLabel={t.deleteUser}
        isConfirming={deleting}
        message={t.deleteUserConfirm.replace('{name}', deleteTarget?.fullName ?? '')}
        onCancel={() => setDeleteTarget(undefined)}
        onConfirm={() => void deleteUser()}
        open={Boolean(deleteTarget)}
        title={t.deleteUser}
      />
    </div>
  );
}

function AccessReviewModal({
  open, user, refs, language, onClose, onSaved,
}: {
  open: boolean; user: AdminUser; refs: AdminReferenceData; language: 'ar' | 'en'; onClose: () => void; onSaved: (user: AdminUser) => void;
}) {
  const t = copy[language];
  const toast = useToast();
  const [roleIds, setRoleIds] = useState<string[]>([]);
  const [grants, setGrants] = useState<SaveAccessGrant[]>([]);
  const [collectionCodes, setCollectionCodes] = useState<string[]>([]);
  const [collectionScope, setCollectionScope] = useState('TEAM');
  const [collectionClients, setCollectionClients] = useState<string[]>([]);
  const [collectionExpiry, setCollectionExpiry] = useState('');
  const [module, setModule] = useState('ALL');
  const [query, setQuery] = useState('');
  const [phrase, setPhrase] = useState('');
  const [saving, setSaving] = useState(false);
  const [employeeId, setEmployeeId] = useState(user.employeeId ?? '');
  const [employees, setEmployees] = useState<AdminLinkableEmployee[]>([]);

  useEffect(() => {
    if (!open) return;
    setRoleIds(user.roles.map((role) => role.id));
    const extras = user.accessGrants.filter((grant) => grant.status !== 'REVOKED');
    setGrants(extras.filter((grant) => !grant.permissionCode.startsWith('collections.')).map(toDraftGrant));
    const collections = extras.filter((grant) => grant.permissionCode.startsWith('collections.'));
    setCollectionCodes([...new Set(collections.map((grant) => grant.permissionCode))]);
    setCollectionScope(collections[0]?.scopeType ?? 'TEAM');
    setCollectionClients([...new Set(collections.map((grant) => grant.clientOrganizationId).filter(Boolean) as string[])]);
    setCollectionExpiry(collections[0]?.expiresAt?.slice(0, 10) ?? '');
    setModule('ALL');
    setQuery('');
    setPhrase('');
    setEmployeeId(user.employeeId ?? '');
    void adminService.linkableEmployees({ includeEmployeeId: user.employeeId }).then(setEmployees).catch(() => setEmployees([]));
  }, [open, user]);

  const selectedRoles = refs.roles.filter((role) => roleIds.includes(role.id));
  const included = impliedPermissionCodes(selectedRoles.map((role) => role.name));
  const adminRoleSelected = selectedRoles.some((role) => role.name === 'Admin');
  const extraPermissions = refs.permissions.filter((permission) => grants.some((grant) => grant.permissionCode === permission.code) || collectionCodes.includes(permission.code));
  const high = extraPermissions.filter((permission) => permission.riskLevel === 'HIGH' || permission.riskLevel === 'CRITICAL').length + (adminRoleSelected ? 1 : 0);
  const clientMissing = collectionCodes.length > 0 && collectionScope === 'CLIENT' && collectionClients.length === 0;
  const grantClientMissing = grants.some((grant) => grant.scopeType === 'CLIENT' && !grant.clientOrganizationId);

  const visiblePermissions = useMemo(() => {
    const term = query.trim().toLowerCase();
    return refs.permissions.filter((permission) => {
      if (module !== 'ALL' && permission.group !== module) return false;
      if (!term) return true;
      return [permission.code, permission.nameAr, permission.nameEn, permission.descriptionAr, permission.descriptionEn].some((value) => value.toLowerCase().includes(term));
    });
  }, [module, query, refs.permissions]);

  const grouped = GROUP_ORDER
    .map((group) => ({ group, items: visiblePermissions.filter((permission) => permission.group === group) }))
    .filter((entry) => entry.items.length);

  const toggleRole = (id: string) => setRoleIds((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id]);
  const togglePermission = (permission: AdminPermission) => {
    if (permission.group === 'COLLECTIONS') {
      setCollectionCodes((current) => current.includes(permission.code) ? current.filter((code) => code !== permission.code) : [...current, permission.code]);
      if (permission.allowedScopes.includes(collectionScope) === false) setCollectionScope(permission.allowedScopes[0]);
      return;
    }
    setGrants((current) => current.some((grant) => grant.permissionCode === permission.code)
      ? current.filter((grant) => grant.permissionCode !== permission.code)
      : [...current, { permissionCode: permission.code, scopeType: permission.allowedScopes[0] }]);
  };
  const updateGrant = (code: string, changes: Partial<SaveAccessGrant>) => setGrants((current) => current.map((grant) => grant.permissionCode === code ? { ...grant, ...changes } : grant));
  const changeScope = (code: string, scopeType: string) => setGrants((current) => {
    const first = current.find((grant) => grant.permissionCode === code);
    return [...current.filter((grant) => grant.permissionCode !== code), { permissionCode: code, scopeType, expiresAt: first?.expiresAt }];
  });
  const toggleClient = (code: string, clientId: string, checked: boolean) => setGrants((current) => {
    const rest = current.filter((grant) => grant.permissionCode !== code);
    const same = current.filter((grant) => grant.permissionCode === code);
    const expiry = same[0]?.expiresAt;
    const clients = same.filter((grant) => grant.scopeType === 'CLIENT' && grant.clientOrganizationId !== clientId);
    if (checked) clients.push({ permissionCode: code, scopeType: 'CLIENT', clientOrganizationId: clientId, expiresAt: expiry });
    return [...rest, ...clients];
  });

  const submit = async () => {
    setSaving(true);
    try {
      const collectionGrants = buildCollectionGrants(collectionCodes, collectionScope, collectionClients, collectionExpiry);
      const payload = [...grants, ...collectionGrants]
        .flatMap((grant) => grant.scopeType === 'CLIENT' && !grant.clientOrganizationId ? [] : [grant])
        .map((grant) => ({ ...grant, expiresAt: grant.expiresAt ? new Date(`${grant.expiresAt}T23:59:59.999Z`).toISOString() : undefined }));
      let result = await adminService.saveAccess(user.id, {
        roleIds,
        grants: payload,
        confirmationPhrase: adminRoleSelected ? phrase : '',
      });
      if ((employeeId || '') !== (user.employeeId || '')) {
        result = await adminService.linkEmployee(user.id, employeeId || null);
      }
      onSaved(result);
    } catch (error) {
      toast.error(getApiErrorMessage(error, language === 'ar' ? 'تعذر اعتماد الصلاحيات. راجع البيانات وحاول مرة أخرى.' : 'Could not authorize access. Review the details and try again.'));
    } finally {
      setSaving(false);
    }
  };

  const scopeLabel = (scope: string) => scope === 'OWN' ? t.own : scope === 'TEAM' ? t.team : scope === 'DEPARTMENT' ? t.dept : scope === 'CLIENT' ? t.client : t.all;

  return (
    <Modal
      bodyClassName="p-0"
      className="h-[calc(100vh-2rem)]"
      description={`${t.accessDesc} ${user.loginCode}`}
      footer={(
        <>
          <Button fullWidth={false} onClick={onClose} size="md" variant="outline">{t.cancel}</Button>
          <Button disabled={(adminRoleSelected && phrase !== 'GRANT ADMIN ACCESS') || clientMissing || grantClientMissing} fullWidth={false} isLoading={saving} leftIcon={<ShieldCheck className="h-4 w-4" />} onClick={submit} size="md">{t.save}</Button>
        </>
      )}
      onClose={onClose}
      open={open}
      size="full"
      title={`${t.accessTitle} — ${user.fullName}`}
    >
      <div className="grid h-full min-h-0 overflow-y-auto xl:grid-cols-[320px_1fr_320px] xl:overflow-hidden">
        <aside className="border-e border-slate-200 bg-slate-50 p-5 xl:min-h-0 xl:overflow-y-auto">
          <p className="text-xs font-black uppercase tracking-wider text-slate-400">{t.employee}</p>
          <p className="mt-2 text-xs leading-5 text-slate-500">{t.employeeHelp}</p>
          <label className="mt-3 block text-xs font-bold text-slate-700">
            <ProfessionalSelect className="field mt-1 !py-2 text-xs" onChange={(event) => setEmployeeId(event.target.value)} value={employeeId}>
              <option value="">{t.noEmployee}</option>
              {employees.map((item) => <option key={item.id} value={item.id}>{item.employeeNumber} · {item.fullName}</option>)}
            </ProfessionalSelect>
          </label>
          <p className="text-xs font-black uppercase tracking-wider text-slate-400 mt-6">{t.roleStep}</p>
          <p className="mt-2 text-xs leading-5 text-slate-500">{t.roleHint}</p>
          <div className="mt-4 space-y-2">
            {refs.roles.map((role) => {
              const selected = roleIds.includes(role.id);
              return (
                <button
                  className={`w-full rounded-2xl border p-3 text-start transition ${selected ? 'border-cyan-400 bg-white shadow-sm' : 'border-transparent hover:bg-white'}`}
                  key={role.id}
                  onClick={() => toggleRole(role.id)}
                  type="button"
                >
                  <span className="flex items-start gap-3">
                    <span className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border ${selected ? 'border-cyan-600 bg-cyan-600 text-white' : 'border-slate-300'}`}>{selected && <Check className="h-3.5 w-3.5" />}</span>
                    <span>
                      <span className="block text-sm font-bold text-slate-800">{roleLabel(role.name, language)}</span>
                      <span className="mt-1 block text-xs leading-5 text-slate-500">{roleSummary(role.name, language) || role.description}</span>
                    </span>
                  </span>
                </button>
              );
            })}
          </div>
        </aside>
        <main className="p-5 sm:p-6 xl:min-h-0 xl:overflow-y-auto">
          <p className="text-xs font-black uppercase tracking-wider text-slate-400">{t.extraStep}</p>
          <p className="mt-2 text-sm leading-6 text-slate-500">{t.extraHint}</p>
          <div className="mt-4 flex flex-wrap gap-2">
            <Chip active={module === 'ALL'} label={t.allModules} onClick={() => setModule('ALL')} />
            {GROUP_ORDER.map((group) => <Chip active={module === group} key={group} label={PERMISSION_GROUPS[group][language]} onClick={() => setModule(group)} />)}
          </div>
          <label className="relative mt-4 block">
            <Search className="absolute start-3 top-3 h-4 w-4 text-slate-400" />
            <input className="field !py-2.5 ps-10" onChange={(event) => setQuery(event.target.value)} placeholder={t.searchPermissions} value={query} />
          </label>
          <div className="mt-5 space-y-7">
            {grouped.map(({ group, items }) => {
              const meta = PERMISSION_GROUPS[group];
              const isCollections = group === 'COLLECTIONS';
              return (
                <section key={group}>
                  <div className="mb-3 flex flex-wrap items-center gap-2">
                    <h3 className="font-black text-[#09263d]">{meta[language]}</h3>
                    {meta.planned && <span className="rounded-full bg-slate-100 px-2 py-1 text-[10px] font-bold text-slate-500">{t.planned}</span>}
                  </div>
                  {isCollections && (
                    <div className="mb-4 rounded-2xl border border-cyan-200 bg-cyan-50/60 p-4">
                      <p className="text-xs font-bold leading-5 text-cyan-950">{t.collectionsHelp}</p>
                      <div className="mt-3 grid gap-3 sm:grid-cols-2">
                        <label className="text-xs font-bold text-slate-700">{t.scope}
                          <ProfessionalSelect className="field mt-1 !py-2 text-xs" onChange={(event) => setCollectionScope(event.target.value)} value={collectionScope}>
                            {['OWN', 'TEAM', 'CLIENT', 'ALL'].map((scope) => <option key={scope} value={scope}>{scopeLabel(scope)}</option>)}
                          </ProfessionalSelect>
                        </label>
                        <label className="text-xs font-bold text-slate-700">{t.expires}
                          <DateControl className="field mt-1 !py-2 text-xs" onChange={(event) => setCollectionExpiry(event.target.value)} value={collectionExpiry} />
                        </label>
                      </div>
                      {collectionScope === 'CLIENT' && (
                        <ClientPicker clients={refs.clients} language={language} onToggle={(id, checked) => setCollectionClients((current) => checked ? [...current, id] : current.filter((item) => item !== id))} selected={collectionClients} title={t.clients} />
                      )}
                    </div>
                  )}
                  <div className="grid gap-3 lg:grid-cols-2">
                    {items.map((permission) => {
                      const selected = permission.group === 'COLLECTIONS' ? collectionCodes.includes(permission.code) : grants.some((grant) => grant.permissionCode === permission.code);
                      const grant = grants.find((item) => item.permissionCode === permission.code);
                      const clientIds = grants.filter((item) => item.permissionCode === permission.code && item.scopeType === 'CLIENT').map((item) => item.clientOrganizationId);
                      const packed = included.has(permission.code) || included.has('*');
                      return (
                        <article className={`rounded-xl border p-4 transition ${selected ? 'border-cyan-300 bg-cyan-50/30' : 'border-slate-200'}`} key={permission.code}>
                          <button className="flex w-full items-start gap-3 text-start" onClick={() => togglePermission(permission)} type="button">
                            <span className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded border ${selected ? 'border-cyan-600 bg-cyan-600 text-white' : 'border-slate-300'}`}>{selected && <Check className="h-3.5 w-3.5" />}</span>
                            <span className="min-w-0 flex-1">
                              <span className="flex items-center justify-between gap-2">
                                <strong className="text-sm text-slate-900">{language === 'ar' ? permission.nameAr : permission.nameEn}</strong>
                                <RiskBadge language={language} risk={permission.riskLevel} />
                              </span>
                              <span className="mt-1 block text-xs leading-5 text-slate-500">{language === 'ar' ? permission.descriptionAr : permission.descriptionEn}</span>
                              {packed && <span className="mt-2 inline-flex rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-800">{t.included}</span>}
                            </span>
                          </button>
                          {selected && permission.group !== 'COLLECTIONS' && (
                            <div className="mt-4 grid gap-3 border-t border-slate-200 pt-3 sm:grid-cols-2">
                              <label className="text-xs font-bold text-slate-600">{t.scope}
                                <ProfessionalSelect className="field mt-1 !py-2 text-xs" onChange={(event) => changeScope(permission.code, event.target.value)} value={grant?.scopeType ?? permission.allowedScopes[0]}>
                                  {permission.allowedScopes.map((scope) => <option key={scope} value={scope}>{scopeLabel(scope)}</option>)}
                                </ProfessionalSelect>
                              </label>
                              <label className="text-xs font-bold text-slate-600">{t.expires}
                                <DateControl className="field mt-1 !py-2 text-xs" onChange={(event) => updateGrant(permission.code, { expiresAt: event.target.value || undefined })} value={grant?.expiresAt ?? ''} />
                              </label>
                              {grant?.scopeType === 'CLIENT' && (
                                <div className="sm:col-span-2">
                                  <ClientPicker clients={refs.clients} language={language} onToggle={(id, checked) => toggleClient(permission.code, id, checked)} selected={clientIds.filter(Boolean) as string[]} title={t.clients} />
                                </div>
                              )}
                            </div>
                          )}
                        </article>
                      );
                    })}
                  </div>
                </section>
              );
            })}
          </div>
        </main>
        <aside className="overflow-y-auto border-s border-slate-200 bg-[#f8fafc] p-5">
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
            <div className="flex gap-3">
              <AlertOctagon className="h-5 w-5 shrink-0 text-amber-700" />
              <div>
                <h3 className="font-black text-amber-900">{t.impact}</h3>
                <p className="mt-2 text-xs leading-6 text-amber-900/80">{t.criticalWarning}</p>
              </div>
            </div>
          </div>
          {adminRoleSelected && <div className="mt-3 rounded-xl border-2 border-rose-300 bg-rose-50 p-4 text-xs font-bold leading-6 text-rose-900"><AlertOctagon className="mb-2 h-5 w-5" />{t.adminRoleWarning}</div>}
          <div className="mt-4 grid grid-cols-2 gap-3">
            <div className="rounded-xl border border-slate-200 bg-white p-3"><p className="text-2xl font-black text-[#09263d]">{selectedRoles.length}</p><p className="text-xs text-slate-500">{t.rolesCount}</p></div>
            <div className="rounded-xl border border-slate-200 bg-white p-3"><p className="text-2xl font-black text-[#09263d]">{extraPermissions.length}</p><p className="text-xs text-slate-500">{t.selected}</p></div>
          </div>
          <div className="mt-3 rounded-xl border border-rose-200 bg-white p-3"><p className="text-2xl font-black text-rose-700">{high}</p><p className="text-xs text-slate-500">{t.critical}</p></div>
          <div className="mt-4 space-y-2">
            {selectedRoles.map((role) => <p className="rounded-lg bg-white px-3 py-2 text-xs font-bold text-slate-700" key={role.id}>{roleLabel(role.name, language)}</p>)}
          </div>
          {adminRoleSelected && (
            <label className="mt-5 block text-xs font-black text-rose-800">{t.adminPhrase}
              <input autoComplete="off" className="field mt-2 text-sm" dir="ltr" onChange={(event) => setPhrase(event.target.value)} value={phrase} />
            </label>
          )}
        </aside>
      </div>
    </Modal>
  );
}

function SecurityModal({
  open, user, language, onClose, onSaved, onIssued, onDelete,
}: {
  open: boolean; user: AdminUser; language: 'ar' | 'en'; onClose: () => void; onSaved: (user: AdminUser) => void; onIssued: (issue: AdminCredentialIssue) => void; onDelete: () => void;
}) {
  const t = copy[language];
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  const act = async (kind: 'status' | 'password') => {
    setBusy(true);
    try {
      if (kind === 'password') onIssued(await adminService.resetPassword(user.id));
      else onSaved(await adminService.setStatus(user.id, !user.isActive));
    } catch (error) {
      toast.error(getApiErrorMessage(error, language === 'ar' ? 'تعذر تنفيذ الإجراء.' : 'Could not complete the action.'));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Modal onClose={onClose} open={open} size="md" title={`${t.statusTitle} — ${user.fullName}`}>
      <div className="rounded-xl border border-sky-200 bg-sky-50 p-4 text-sm leading-6 text-sky-900">{t.statusWarning}</div>
      {user.mustChangePassword && <p className="mt-3 rounded-xl bg-amber-50 px-4 py-3 text-sm font-bold text-amber-900">{t.tempBadge}</p>}
      <div className="mt-5 rounded-xl border border-slate-200 p-4">
        <p className="font-black text-slate-900">{t.generate}</p>
        <p className="mt-2 text-sm leading-6 text-slate-500">{t.passwordOnce}</p>
        <Button className="mt-4" fullWidth={false} isLoading={busy} leftIcon={<LockKeyhole className="h-4 w-4" />} onClick={() => act('password')} size="md" variant="outline">{t.generate}</Button>
      </div>
      <Button className="mt-5" fullWidth={false} isLoading={busy} leftIcon={user.isActive ? <UserX className="h-4 w-4" /> : <UserCheck className="h-4 w-4" />} onClick={() => act('status')} size="md" variant={user.isActive ? 'danger' : 'primary'}>{user.isActive ? t.suspend : t.activate}</Button>
      <Button className="mt-3" fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={onDelete} size="md" variant="outline">{t.deleteUser}</Button>
    </Modal>
  );
}

function CreateUserModal({
  open, refs, language, onClose, onCreated,
}: {
  open: boolean; refs: AdminReferenceData; language: 'ar' | 'en'; onClose: () => void; onCreated: (issue: AdminCredentialIssue) => void;
}) {
  const t = copy[language];
  const toast = useToast();
  const jobRoles = refs.roles.filter((role) => role.name !== 'Admin');
  const [fullName, setFullName] = useState('');
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [departmentId, setDepartmentId] = useState(refs.departments[0]?.id ?? '');
  const [roleId, setRoleId] = useState('');
  const [usernameTouched, setUsernameTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [employeeId, setEmployeeId] = useState('');
  const [employees, setEmployees] = useState<AdminLinkableEmployee[]>([]);

  useEffect(() => {
    if (!open) return;
    const first = refs.departments[0];
    setFullName('');
    setUsername('');
    setEmail('');
    setDepartmentId(first?.id ?? '');
    setRoleId(jobRoles.find((role) => role.name === DEPARTMENT_ROLE_HINT[first?.code ?? ''])?.id ?? '');
    setUsernameTouched(false);
    setEmployeeId('');
    void adminService.linkableEmployees().then(setEmployees).catch(() => setEmployees([]));
  }, [open, refs.departments]);

  const applyEmployee = (id: string) => {
    setEmployeeId(id);
    const employee = employees.find((item) => item.id === id);
    if (!employee) return;
    setFullName(employee.fullName);
    if (employee.email) setEmail(employee.email);
    if (!usernameTouched) setUsername(suggestUsername(employee.fullName));
    if (employee.departmentId) changeDepartment(employee.departmentId);
    const hint = employee.operationalRole === 'COLLECTOR' ? 'CollectionsCollector' : employee.operationalRole === 'SUPERVISOR' ? 'CollectionsSupervisor' : DEPARTMENT_ROLE_HINT[employee.departmentCode];
    const match = jobRoles.find((role) => role.name === hint);
    if (match) setRoleId(match.id);
  };

  const changeDepartment = (id: string) => {
    setDepartmentId(id);
    const code = refs.departments.find((item) => item.id === id)?.code ?? '';
    const hint = DEPARTMENT_ROLE_HINT[code];
    const match = jobRoles.find((role) => role.name === hint);
    if (match) setRoleId(match.id);
  };

  const submit = async () => {
    setBusy(true);
    try {
      onCreated(await adminService.createUser({
        fullName,
        username,
        email,
        departmentId,
        roleIds: roleId ? [roleId] : [],
        employeeId: employeeId || undefined,
      }));
    } catch (error) {
      toast.error(getApiErrorMessage(error, language === 'ar' ? 'تعذر إنشاء المستخدم.' : 'Could not create user.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      footer={(
        <>
          <Button fullWidth={false} onClick={onClose} size="md" variant="outline">{t.cancel}</Button>
          <Button disabled={!fullName || !username || !email || !departmentId} fullWidth={false} isLoading={busy} onClick={submit} size="md">{t.create}</Button>
        </>
      )}
      onClose={onClose}
      open={open}
      size="lg"
      title={t.createTitle}
    >
      <p className="rounded-xl bg-slate-50 px-4 py-3 text-sm leading-6 text-slate-600">{t.createLead}</p>
      <p className="mt-3 text-xs leading-5 text-slate-500">{t.employeeHelp}</p>
      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        <label className="text-sm font-bold text-slate-700 sm:col-span-2">{t.employee}
          <ProfessionalSelect className="field mt-2" onChange={(event) => applyEmployee(event.target.value)} value={employeeId}>
            <option value="">{t.noEmployee}</option>
            {employees.map((item) => <option key={item.id} value={item.id}>{item.employeeNumber} · {item.fullName}</option>)}
          </ProfessionalSelect>
        </label>
        <label className="text-sm font-bold text-slate-700">{t.fullName}
          <input className="field mt-2" onChange={(event) => { setFullName(event.target.value); if (!usernameTouched) setUsername(suggestUsername(event.target.value)); }} value={fullName} />
        </label>
        <label className="text-sm font-bold text-slate-700">{t.username}
          <input className="field mt-2" dir="ltr" onChange={(event) => { setUsername(event.target.value); setUsernameTouched(true); }} value={username} />
        </label>
        <label className="text-sm font-bold text-slate-700">{t.email}
          <input className="field mt-2" dir="ltr" onChange={(event) => setEmail(event.target.value)} type="email" value={email} />
        </label>
        <label className="text-sm font-bold text-slate-700">{t.department}
          <ProfessionalSelect className="field mt-2" onChange={(event) => changeDepartment(event.target.value)} value={departmentId}>
            {refs.departments.map((item) => <option key={item.id} value={item.id}>{language === 'ar' ? item.nameAr : item.nameEn}</option>)}
          </ProfessionalSelect>
        </label>
        <label className="text-sm font-bold text-slate-700 sm:col-span-2">{t.startingRole}
          <ProfessionalSelect className="field mt-2" onChange={(event) => setRoleId(event.target.value)} value={roleId}>
            <option value="">{t.noRole}</option>
            {jobRoles.map((role) => <option key={role.id} value={role.id}>{roleLabel(role.name, language)}</option>)}
          </ProfessionalSelect>
        </label>
      </div>
    </Modal>
  );
}

function IssuedCredentialsModal({
  open, issue, language, onClose, onAssign,
}: {
  open: boolean; issue: AdminCredentialIssue; language: 'ar' | 'en'; onClose: () => void; onAssign: () => void;
}) {
  const t = copy[language];
  const [copied, setCopied] = useState('');
  const copyValue = async (key: string, value: string) => {
    await navigator.clipboard.writeText(value);
    setCopied(key);
    window.setTimeout(() => setCopied(''), 1500);
  };
  const rows = [
    { key: 'code', label: t.loginCode, value: issue.user.loginCode },
    { key: 'username', label: t.username, value: issue.user.username },
    { key: 'password', label: t.password, value: issue.temporaryPassword },
  ];
  return (
    <Modal
      footer={(
        <>
          <Button fullWidth={false} onClick={onClose} size="md" variant="outline">{t.done}</Button>
          <Button fullWidth={false} onClick={onAssign} size="md">{t.nextAccess}</Button>
        </>
      )}
      onClose={onClose}
      open={open}
      size="md"
      title={t.issuedTitle}
    >
      <p className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm leading-6 text-amber-950">{t.issuedHelp}</p>
      <p className="mt-3 text-sm font-bold text-slate-800">{issue.user.fullName}</p>
      <div className="mt-4 space-y-3">
        {rows.map((row) => (
          <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3" key={row.key}>
            <div>
              <p className="text-xs font-semibold text-slate-500">{row.label}</p>
              <p className="mt-1 font-mono text-sm font-bold text-slate-900" dir="ltr">{row.value}</p>
            </div>
            <button className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-xs font-bold text-mis-primary hover:bg-white" onClick={() => copyValue(row.key, row.value)} type="button">
              {copied === row.key ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
              {copied === row.key ? t.copied : t.copy}
            </button>
          </div>
        ))}
      </div>
    </Modal>
  );
}

function Chip({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return <button className={`rounded-full px-3 py-1.5 text-xs font-bold ${active ? 'bg-[#09263d] text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`} onClick={onClick} type="button">{label}</button>;
}

function ClientPicker({
  title, clients, selected, language, onToggle,
}: {
  title: string; clients: AdminClient[]; selected: string[]; language: 'ar' | 'en'; onToggle: (id: string, checked: boolean) => void;
}) {
  return (
    <div className="sm:col-span-2">
      <p className="text-xs font-bold text-slate-600">{title}</p>
      <div className="mt-2 grid max-h-48 gap-2 overflow-y-auto sm:grid-cols-2">
        {clients.filter((client) => client.isActive).map((client) => (
          <label className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-xs ${selected.includes(client.id) ? 'border-cyan-300 bg-white text-cyan-900' : 'border-slate-200 bg-white text-slate-600'}`} key={client.id}>
            <input checked={selected.includes(client.id)} onChange={(event) => onToggle(client.id, event.target.checked)} type="checkbox" />
            <span>{language === 'ar' ? client.nameAr : client.nameEn}</span>
          </label>
        ))}
      </div>
    </div>
  );
}

function RiskBadge({ risk, language }: { risk: AdminPermission['riskLevel']; language: 'ar' | 'en' }) {
  const t = copy[language];
  const label = risk === 'LOW' ? t.low : risk === 'MEDIUM' ? t.medium : risk === 'HIGH' ? t.high : t.criticalRisk;
  const cls = risk === 'CRITICAL' ? 'bg-rose-100 text-rose-700' : risk === 'HIGH' ? 'bg-amber-100 text-amber-700' : risk === 'MEDIUM' ? 'bg-sky-100 text-sky-700' : 'bg-slate-100 text-slate-600';
  return <span className={`shrink-0 rounded-full px-2 py-1 text-[10px] font-black ${cls}`}>{label}</span>;
}

function toDraftGrant(grant: AdminUser['accessGrants'][number]): SaveAccessGrant {
  return { permissionCode: grant.permissionCode, scopeType: grant.scopeType, clientOrganizationId: grant.clientOrganizationId, expiresAt: grant.expiresAt?.slice(0, 10) };
}

function buildCollectionGrants(codes: string[], scope: string, clients: string[], expiry: string): SaveAccessGrant[] {
  const expiresAt = expiry || undefined;
  if (scope === 'CLIENT') {
    return codes.flatMap((permissionCode) => clients.map((clientOrganizationId) => ({ permissionCode, scopeType: 'CLIENT', clientOrganizationId, expiresAt })));
  }
  return codes.map((permissionCode) => ({ permissionCode, scopeType: scope, expiresAt }));
}
