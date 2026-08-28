import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { LoginPage } from '../pages/auth/LoginPage';
import { HrLayout } from '../components/layout/HrLayout';
import { CollectionsLayout } from '../components/layout/CollectionsLayout';
import { AdminLayout } from '../components/layout/AdminLayout';
import { FinanceLayout } from '../components/layout/FinanceLayout';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { UnauthorizedPage } from '../pages/UnauthorizedPage';
import { DepartmentHome } from './DepartmentHome';
import { ProtectedRoute } from './ProtectedRoute';

const ModuleSelectorPage = lazy(() => import('../pages/modules/ModuleSelectorPage').then((module) => ({ default: module.ModuleSelectorPage })));

const HrDashboardPage = lazy(() => import('../pages/hr/HrDashboardPage').then((module) => ({ default: module.HrDashboardPage })));
const HrEmployeesPage = lazy(() => import('../pages/hr/HrEmployeesPage').then((module) => ({ default: module.HrEmployeesPage })));
const HrEmployeeProfilePage = lazy(() => import('../pages/hr/HrEmployeeProfilePage').then((module) => ({ default: module.HrEmployeeProfilePage })));
const HrAttendancePage = lazy(() => import('../pages/hr/HrAttendancePage').then((module) => ({ default: module.HrAttendancePage })));
const HrAttendanceImportPage = lazy(() => import('../pages/hr/HrAttendanceImportPage').then((module) => ({ default: module.HrAttendanceImportPage })));
const HrLeavesPage = lazy(() => import('../pages/hr/HrLeavesPage').then((module) => ({ default: module.HrLeavesPage })));
const HrCalendarPage = lazy(() => import('../pages/hr/HrCalendarPage').then((module) => ({ default: module.HrCalendarPage })));
const HrReportsPage = lazy(() => import('../pages/hr/HrReportsPage').then((module) => ({ default: module.HrReportsPage })));
const HrDelegationsPage = lazy(() => import('../pages/hr/HrDelegationsPage').then((module) => ({ default: module.HrDelegationsPage })));
const HrAbsencesPage = lazy(() => import('../pages/hr/HrAbsencesPage').then((module) => ({ default: module.HrAbsencesPage })));
const HrEmployeeDocumentsPage = lazy(() => import('../pages/hr/HrEmployeeDocumentsPage').then((module) => ({ default: module.HrEmployeeDocumentsPage })));
const HrMasterPage = lazy(() => import('../pages/hr/HrMasterPage').then((module) => ({ default: module.HrMasterPage })));
const HrAuditPage = lazy(() => import('../pages/hr/HrAuditPage').then((module) => ({ default: module.HrAuditPage })));
const CollectionsDashboardPage = lazy(() => import('../pages/collections/CollectionsDashboardPage').then((module) => ({ default: module.CollectionsDashboardPage })));
const CollectionsClientsPage = lazy(() => import('../pages/collections/CollectionsClientsPage').then((module) => ({ default: module.CollectionsClientsPage })));
const CollectionClientWorkspacePage = lazy(() => import('../pages/collections/CollectionClientWorkspacePage').then((module) => ({ default: module.CollectionClientWorkspacePage })));
const CollectionCasesPage = lazy(() => import('../pages/collections/CollectionCasesPage').then((module) => ({ default: module.CollectionCasesPage })));
const CollectionCaseDetailsPage = lazy(() => import('../pages/collections/CollectionCaseDetailsPage').then((module) => ({ default: module.CollectionCaseDetailsPage })));
const CollectionPromisesPage = lazy(() => import('../pages/collections/CollectionPromisesPage').then((module) => ({ default: module.CollectionPromisesPage })));
const CollectionPaymentsPage = lazy(() => import('../pages/collections/CollectionPaymentsPage').then((module) => ({ default: module.CollectionPaymentsPage })));
const CollectionAssignmentsPage = lazy(() => import('../pages/collections/CollectionAssignmentsPage').then((module) => ({ default: module.CollectionAssignmentsPage })));
const CollectionVisitsPage = lazy(() => import('../pages/collections/CollectionVisitsPage').then((module) => ({ default: module.CollectionVisitsPage })));
const CollectionComplaintsPage = lazy(() => import('../pages/collections/CollectionComplaintsPage').then((module) => ({ default: module.CollectionComplaintsPage })));
const CollectionAuditPage = lazy(() => import('../pages/collections/CollectionAuditPage').then((module) => ({ default: module.CollectionAuditPage })));
const CollectionImportsPage = lazy(() => import('../pages/collections/CollectionImportsPage').then((module) => ({ default: module.CollectionImportsPage })));
const CollectionsSettingsPage = lazy(() => import('../pages/collections/CollectionsSettingsPage').then((module) => ({ default: module.CollectionsSettingsPage })));
const CollectionsReportsPage = lazy(() => import('../pages/collections/CollectionsReportsPage').then((module) => ({ default: module.CollectionsReportsPage })));
const CollectionsBrandingPage = lazy(() => import('../pages/collections/CollectionsBrandingPage').then((module) => ({ default: module.CollectionsBrandingPage })));
const BanksPage = lazy(() => import('../pages/banks/BanksPage').then((module) => ({ default: module.BanksPage })));
const InstallmentCompaniesPage = lazy(() => import('../pages/banks/InstallmentCompaniesPage').then((module) => ({ default: module.InstallmentCompaniesPage })));
const BankWorkspaceLayout = lazy(() => import('../pages/banks/BankWorkspaceLayout').then((module) => ({ default: module.BankWorkspaceLayout })));
const BankWorkspaceSectionPage = lazy(() => import('../pages/banks/BankWorkspaceSectionPage').then((module) => ({ default: module.BankWorkspaceSectionPage })));
const BankPortfolioImportPage = lazy(() => import('../pages/banks/BankPortfolioImportPage').then((module) => ({ default: module.BankPortfolioImportPage })));
const BankPortfolioManagementPage = lazy(() => import('../pages/banks/BankPortfolioManagementPage').then((module) => ({ default: module.BankPortfolioManagementPage })));
const BankCaseDistributionPage = lazy(() => import('../pages/banks/BankCaseDistributionPage').then((module) => ({ default: module.BankCaseDistributionPage })));
const BankCaseActivityCenterPage = lazy(() => import('../pages/banks/BankCaseActivityCenterPage').then((module) => ({ default: module.BankCaseActivityCenterPage })));
const BankPtpCenterPage = lazy(() => import('../pages/banks/BankPtpCenterPage').then((module) => ({ default: module.BankPtpCenterPage })));
const BankVisitsManagementPage = lazy(() => import('../pages/banks/BankVisitsManagementPage').then((module) => ({ default: module.BankVisitsManagementPage })));
const BankDcrPage = lazy(() => import('../pages/banks/BankDcrPage').then((module) => ({ default: module.BankDcrPage })));
const BankComplaintsManagementPage = lazy(() => import('../pages/banks/BankComplaintsManagementPage').then((module) => ({ default: module.BankComplaintsManagementPage })));
const BankArchivePage = lazy(() => import('../pages/banks/BankArchivePage').then((module) => ({ default: module.BankArchivePage })));
const AccountProfilePage = lazy(() => import('../pages/profile/AccountProfilePage').then((module) => ({ default: module.AccountProfilePage })));
const AdminDashboardPage = lazy(() => import('../pages/admin/AdminDashboardPage').then((module) => ({ default: module.AdminDashboardPage })));
const AdminUsersPage = lazy(() => import('../pages/admin/AdminUsersPage').then((module) => ({ default: module.AdminUsersPage })));
const AdminAuditPage = lazy(() => import('../pages/admin/AdminAuditPage').then((module) => ({ default: module.AdminAuditPage })));
const FinanceDashboardPage = lazy(() => import('../pages/finance/FinanceDashboardPage').then((module) => ({ default: module.FinanceDashboardPage })));
const FinanceJournalsPage = lazy(() => import('../pages/finance/FinanceJournalsPage').then((module) => ({ default: module.FinanceJournalsPage })));
const FinanceJournalDetailsPage = lazy(() => import('../pages/finance/FinanceJournalDetailsPage').then((module) => ({ default: module.FinanceJournalDetailsPage })));
const FinanceAccountsPage = lazy(() => import('../pages/finance/FinanceAccountsPage').then((module) => ({ default: module.FinanceAccountsPage })));
const FinancePeriodsPage = lazy(() => import('../pages/finance/FinancePeriodsPage').then((module) => ({ default: module.FinancePeriodsPage })));
const FinanceReportsPage = lazy(() => import('../pages/finance/FinanceReportsPage').then((module) => ({ default: module.FinanceReportsPage })));
const FinanceCollectionsPage = lazy(() => import('../pages/finance/FinanceCollectionsPage').then((module) => ({ default: module.FinanceCollectionsPage })));
const FinanceCustodyPage = lazy(() => import('../pages/finance/FinanceCustodyPage').then((module) => ({ default: module.FinanceCustodyPage })));

function HrRouteBoundary() {
  return <Suspense fallback={<div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>}><HrLayout /></Suspense>;
}

function CollectionsRouteBoundary() {
  return <Suspense fallback={<div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>}><CollectionsLayout /></Suspense>;
}

function AdminRouteBoundary() {
  return <Suspense fallback={<div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>}><AdminLayout /></Suspense>;
}

function FinanceRouteBoundary() {
  return <Suspense fallback={<div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>}><FinanceLayout /></Suspense>;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<DepartmentHome />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/modules" element={<Suspense fallback={<div className="flex min-h-screen items-center justify-center"><LoadingSpinner /></div>}><ModuleSelectorPage /></Suspense>} />
      </Route>
      <Route element={<ProtectedRoute requiredRole="Admin" />}>
        <Route path="/admin" element={<Navigate to="/admin/dashboard" replace />} />
        <Route path="/admin" element={<AdminRouteBoundary />}>
          <Route path="dashboard" element={<AdminDashboardPage />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="audit" element={<AdminAuditPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="HR" />}>
        <Route path="/hr" element={<Navigate to="/hr/dashboard" replace />} />
        <Route path="/hr" element={<HrRouteBoundary />}>
          <Route path="dashboard" element={<HrDashboardPage />} />
          <Route path="employees" element={<HrEmployeesPage />} />
          <Route path="employees/:id" element={<HrEmployeeProfilePage />} />
          <Route path="attendance" element={<HrAttendancePage />} />
          <Route path="attendance/import" element={<HrAttendanceImportPage />} />
          <Route path="leaves" element={<HrLeavesPage />} />
          <Route path="calendar" element={<HrCalendarPage />} />
          <Route path="reports" element={<HrReportsPage />} />
          <Route path="delegations" element={<HrDelegationsPage />} />
          <Route path="absences" element={<HrAbsencesPage />} />
          <Route path="employee-documents" element={<HrEmployeeDocumentsPage />} />
          <Route path="audit" element={<HrAuditPage />} />
          <Route path="master" element={<HrMasterPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="ACCOUNTING" />}>
        <Route path="/finance" element={<Navigate to="/finance/dashboard" replace />} />
        <Route path="/finance" element={<FinanceRouteBoundary />}>
          <Route path="dashboard" element={<FinanceDashboardPage />} />
          <Route path="journals" element={<FinanceJournalsPage />} />
          <Route path="journals/:id" element={<FinanceJournalDetailsPage />} />
          <Route path="collections" element={<FinanceCollectionsPage />} />
          <Route path="custody" element={<FinanceCustodyPage />} />
          <Route path="accounts" element={<FinanceAccountsPage />} />
          <Route path="periods" element={<FinancePeriodsPage />} />
          <Route path="reports" element={<FinanceReportsPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="COLLECTIONS" />}>
        <Route element={<CollectionsRouteBoundary />}>
          <Route path="/banks" element={<BanksPage />} />
          <Route path="/installment-companies/:companyId" element={<BankWorkspaceLayout />}>
            <Route index element={<Navigate to="overview" replace />} />
            <Route path="import" element={<BankPortfolioImportPage />} />
            <Route path="portfolio" element={<BankPortfolioManagementPage />} />
            <Route path="distribution" element={<BankCaseDistributionPage />} />
            <Route path="activity" element={<BankCaseActivityCenterPage />} />
            <Route path="ptp" element={<BankPtpCenterPage />} />
            <Route path="visits" element={<BankVisitsManagementPage />} />
            <Route path="dcr" element={<BankDcrPage />} />
            <Route path="complaints" element={<BankComplaintsManagementPage />} />
            <Route path="archive" element={<BankArchivePage />} />
            <Route path="overview" element={<BankWorkspaceSectionPage />} />
            <Route path="*" element={<Navigate to="overview" replace />} />
          </Route>
          <Route path="/banks/:bankId" element={<BankWorkspaceLayout />}>
            <Route index element={<Navigate to="overview" replace />} />
            <Route path="import" element={<BankPortfolioImportPage />} />
            <Route path="portfolio" element={<BankPortfolioManagementPage />} />
            <Route path="distribution" element={<BankCaseDistributionPage />} />
            <Route path="activity" element={<BankCaseActivityCenterPage />} />
            <Route path="ptp" element={<BankPtpCenterPage />} />
            <Route path="visits" element={<BankVisitsManagementPage />} />
            <Route path="dcr" element={<BankDcrPage />} />
            <Route path="complaints" element={<BankComplaintsManagementPage />} />
            <Route path="archive" element={<BankArchivePage />} />
            {['overview'].map(section => (
              <Route key={section} path={section} element={<BankWorkspaceSectionPage />} />
            ))}
            <Route path="*" element={<Navigate to="overview" replace />} />
          </Route>
        </Route>
        <Route path="/collections" element={<Navigate to="/collections/dashboard" replace />} />
        <Route path="/collections" element={<CollectionsRouteBoundary />}>
          <Route path="dashboard" element={<CollectionsDashboardPage />} />
          <Route path="installment-companies" element={<InstallmentCompaniesPage />} />
          <Route path="clients" element={<CollectionsClientsPage />} />
          <Route path="clients/:id" element={<CollectionClientWorkspacePage />} />
          <Route path="cases" element={<CollectionCasesPage />} />
          <Route path="cases/:id" element={<CollectionCaseDetailsPage />} />
          <Route path="promises" element={<CollectionPromisesPage />} />
          <Route path="payments" element={<CollectionPaymentsPage />} />
          <Route path="assignments" element={<CollectionAssignmentsPage />} />
          <Route path="visits" element={<CollectionVisitsPage />} />
          <Route path="complaints" element={<CollectionComplaintsPage />} />
          <Route path="audit" element={<CollectionAuditPage />} />
          <Route path="imports" element={<CollectionImportsPage />} />
          <Route path="settings" element={<CollectionsSettingsPage />} />
          <Route path="reports" element={<CollectionsReportsPage />} />
          <Route path="branding" element={<CollectionsBrandingPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route path="/unauthorized" element={<UnauthorizedPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
