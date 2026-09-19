import { HrExcusesPage } from '../pages/hr/HrExcusesPage';
import { HrOriginalVisitPage } from '../pages/hr/HrOriginalVisitPage';
import { HrSocialInsuranceImportPage } from '../pages/hr/HrSocialInsuranceImportPage';
import { HrSocialInsurancePage } from '../pages/hr/HrSocialInsurancePage';
import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { LoginPage } from '../pages/auth/LoginPage';
import { HrLayout } from '../components/layout/HrLayout';
import { CollectionsLayout } from '../components/layout/CollectionsLayout';
import { AdminLayout } from '../components/layout/AdminLayout';
import { FinanceLayout } from '../components/layout/FinanceLayout';
import { DataEntryLayout } from '../components/layout/DataEntryLayout';
import { LegalLayout } from '../components/layout/LegalLayout';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { UnauthorizedPage } from '../pages/UnauthorizedPage';
import { DepartmentHome } from './DepartmentHome';
import { ProtectedRoute } from './ProtectedRoute';

const ModuleSelectorPage = lazy(() => import('../pages/modules/ModuleSelectorPage').then((module) => ({ default: module.ModuleSelectorPage })));

const HrDashboardPage = lazy(() => import('../pages/hr/HrDashboardPage').then((module) => ({ default: module.HrDashboardPage })));
const HrEmployeesPage = lazy(() => import('../pages/hr/HrEmployeesPage').then((module) => ({ default: module.HrEmployeesPage })));
const HrEmployeeImportPage = lazy(() => import('../pages/hr/HrEmployeeImportPage').then((module) => ({ default: module.HrEmployeeImportPage })));
const HrEmployeeProfilePage = lazy(() => import('../pages/hr/HrEmployeeProfilePage').then((module) => ({ default: module.HrEmployeeProfilePage })));
const HrAttendancePage = lazy(() => import('../pages/hr/HrAttendancePage').then((module) => ({ default: module.HrAttendancePage })));
const HrAttendanceImportPage = lazy(() => import('../pages/hr/HrAttendanceImportPage').then((module) => ({ default: module.HrAttendanceImportPage })));
const HrLeavesPage = lazy(() => import('../pages/hr/HrLeavesPage').then((module) => ({ default: module.HrLeavesPage })));
const HrCalendarPage = lazy(() => import('../pages/hr/HrCalendarPage').then((module) => ({ default: module.HrCalendarPage })));
const HrReportsPage = lazy(() => import('../pages/hr/HrReportsPage').then((module) => ({ default: module.HrReportsPage })));
const HrPayrollPage = lazy(() => import('../pages/hr/HrPayrollPage').then((module) => ({ default: module.HrPayrollPage })));
const HrDelegationsPage = lazy(() => import('../pages/hr/HrDelegationsPage').then((module) => ({ default: module.HrDelegationsPage })));
const HrAbsenceImportPage = lazy(() => import('../pages/hr/HrAbsenceImportPage').then((module) => ({ default: module.HrAbsenceImportPage })));
const HrEmployeeDocumentsPage = lazy(() => import('../pages/hr/HrEmployeeDocumentsPage').then((module) => ({ default: module.HrEmployeeDocumentsPage })));
const HrMasterPage = lazy(() => import('../pages/hr/HrMasterPage').then((module) => ({ default: module.HrMasterPage })));
const HrAuditPage = lazy(() => import('../pages/hr/HrAuditPage').then((module) => ({ default: module.HrAuditPage })));
const CollectionsDashboardPage = lazy(() => import('../pages/collections/CollectionsDashboardPage').then((module) => ({ default: module.CollectionsDashboardPage })));
const CollectionsCreditorsDashboardPage = lazy(() => import('../pages/collections/CollectionsCreditorsDashboardPage').then((module) => ({ default: module.CollectionsCreditorsDashboardPage })));
const CollectionsCollectorsDashboardPage = lazy(() => import('../pages/collections/CollectionsCollectorsDashboardPage').then((module) => ({ default: module.CollectionsCollectorsDashboardPage })));
const CollectionCasesPage = lazy(() => import('../pages/collections/CollectionCasesPage').then((module) => ({ default: module.CollectionCasesPage })));
const CollectionCaseDetailsPage = lazy(() => import('../pages/collections/CollectionCaseDetailsPage').then((module) => ({ default: module.CollectionCaseDetailsPage })));
const CollectionPromisesPage = lazy(() => import('../pages/collections/CollectionPromisesPage').then((module) => ({ default: module.CollectionPromisesPage })));
const CollectionPaymentsPage = lazy(() => import('../pages/collections/CollectionPaymentsPage').then((module) => ({ default: module.CollectionPaymentsPage })));
const CollectionAssignmentsPage = lazy(() => import('../pages/collections/CollectionAssignmentsPage').then((module) => ({ default: module.CollectionAssignmentsPage })));
const CollectionVisitsPage = lazy(() => import('../pages/collections/CollectionVisitsPage').then((module) => ({ default: module.CollectionVisitsPage })));
const CollectionComplaintsPage = lazy(() => import('../pages/collections/CollectionComplaintsPage').then((module) => ({ default: module.CollectionComplaintsPage })));
const CollectionAuditPage = lazy(() => import('../pages/collections/CollectionAuditPage').then((module) => ({ default: module.CollectionAuditPage })));
const CollectionsSettingsPage = lazy(() => import('../pages/collections/CollectionsSettingsPage').then((module) => ({ default: module.CollectionsSettingsPage })));
const CollectionsReportsPage = lazy(() => import('../pages/collections/CollectionsReportsPage').then((module) => ({ default: module.CollectionsReportsPage })));
const BanksPage = lazy(() => import('../pages/banks/BanksPage').then((module) => ({ default: module.BanksPage })));
const BankClassificationPrimaryPage = lazy(() => import('../pages/banks/ClassifiedOrganizationPages').then((module) => ({ default: module.BankClassificationPrimaryPage })));
const InstallmentClassificationPrimaryPage = lazy(() => import('../pages/banks/ClassifiedOrganizationPages').then((module) => ({ default: module.InstallmentClassificationPrimaryPage })));
const BankClassificationSecondaryPage = lazy(() => import('../pages/banks/ClassifiedOrganizationPages').then((module) => ({ default: module.BankClassificationSecondaryPage })));
const InstallmentClassificationSecondaryPage = lazy(() => import('../pages/banks/ClassifiedOrganizationPages').then((module) => ({ default: module.InstallmentClassificationSecondaryPage })));
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
const BankCustomersPage = lazy(() => import('../pages/banks/BankCustomersPage').then((module) => ({ default: module.BankCustomersPage })));
const BankCustomerDetailsPage = lazy(() => import('../pages/banks/BankCustomerDetailsPage').then((module) => ({ default: module.BankCustomerDetailsPage })));
const BankCustomerImportPage = lazy(() => import('../pages/banks/BankCustomerImportPage').then((module) => ({ default: module.BankCustomerImportPage })));
const AccountProfilePage = lazy(() => import('../pages/profile/AccountProfilePage').then((module) => ({ default: module.AccountProfilePage })));
const ForceChangePasswordPage = lazy(() => import('../pages/auth/ForceChangePasswordPage').then((module) => ({ default: module.ForceChangePasswordPage })));
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
const AccountingSalariesPage = lazy(() => import('../pages/accounting/AccountingSalariesPage').then((module) => ({ default: module.AccountingSalariesPage })));
const AccountingTransportationPage = lazy(() => import('../pages/accounting/AccountingTransportationPage').then((module) => ({ default: module.AccountingTransportationPage })));
const AccountingCollectorCommissionsPage = lazy(() => import('../pages/accounting/AccountingCollectorCommissionsPage').then((module) => ({ default: module.AccountingCollectorCommissionsPage })));
const AccountingSupervisorCommissionsPage = lazy(() => import('../pages/accounting/AccountingSupervisorCommissionsPage').then((module) => ({ default: module.AccountingSupervisorCommissionsPage })));
const AccountingCommissionRulesPage = lazy(() => import('../pages/accounting/AccountingCommissionRulesPage').then((module) => ({ default: module.AccountingCommissionRulesPage })));
const DataEntryDashboardPage = lazy(() => import('../pages/data-entry/DataEntryDashboardPage').then((module) => ({ default: module.DataEntryDashboardPage })));
const DataEntryClientsPage = lazy(() => import('../pages/data-entry/DataEntryClientsPage').then((module) => ({ default: module.DataEntryClientsPage })));
const DataEntryClientDetailsPage = lazy(() => import('../pages/data-entry/DataEntryClientDetailsPage').then((module) => ({ default: module.DataEntryClientDetailsPage })));
const DataEntryImportPage = lazy(() => import('../pages/data-entry/DataEntryImportPage').then((module) => ({ default: module.DataEntryImportPage })));
const DataEntryHistoryPage = lazy(() => import('../pages/data-entry/DataEntryHistoryPage').then((module) => ({ default: module.DataEntryHistoryPage })));
const LegalDashboardPage = lazy(() => import('../pages/legal/LegalDashboardPage').then((module) => ({ default: module.LegalDashboardPage })));
const LegalCasesPage = lazy(() => import('../pages/legal/LegalCasesPage').then((module) => ({ default: module.LegalCasesPage })));
const LegalCaseDetailsPage = lazy(() => import('../pages/legal/LegalCaseDetailsPage').then((module) => ({ default: module.LegalCaseDetailsPage })));
const CollectionsDataBatchesPage = lazy(() => import('../pages/collections/CollectionsDataBatchesPage').then((module) => ({ default: module.CollectionsDataBatchesPage })));

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

function LegacyAccountingRedirect() {
  const { pathname, search } = useLocation();
  const rest = pathname.replace(/^\/accounting/, '') || '/dashboard';
  return <Navigate to={`/finance${rest}${search}`} replace />;
}

function DataEntryRouteBoundary() {
  return <Suspense fallback={<div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>}><DataEntryLayout /></Suspense>;
}

function LegalRouteBoundary() {
  return <Suspense fallback={<div className="flex min-h-[420px] items-center justify-center"><LoadingSpinner /></div>}><LegalLayout /></Suspense>;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<DepartmentHome />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/change-password" element={<Suspense fallback={<div className="flex min-h-screen items-center justify-center"><LoadingSpinner /></div>}><ForceChangePasswordPage /></Suspense>} />
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
          <Route path="social-insurance/import" element={<HrSocialInsuranceImportPage />} />
          <Route path="excuses" element={<HrExcusesPage />} />
          <Route path="excuses/visits/:visitId" element={<HrOriginalVisitPage />} />
          <Route path="social-insurance" element={<HrSocialInsurancePage />} />
          <Route path="dashboard" element={<HrDashboardPage />} />
          <Route path="employees" element={<HrEmployeesPage />} />
          <Route path="employees/import" element={<HrEmployeeImportPage />} />
          <Route path="employees/:id" element={<HrEmployeeProfilePage />} />
          <Route path="attendance" element={<HrAttendancePage />} />
          <Route path="attendance/import" element={<HrAttendanceImportPage />} />
          <Route path="attendance/absences/import" element={<HrAbsenceImportPage />} />
          <Route path="leaves" element={<HrLeavesPage />} />
          <Route path="calendar" element={<HrCalendarPage />} />
          <Route path="reports" element={<HrReportsPage />} />
          <Route path="payroll" element={<HrPayrollPage />} />
          <Route path="delegations" element={<HrDelegationsPage />} />
          <Route path="absences" element={<Navigate to="/hr/attendance?tab=absences" replace />} />
          <Route path="absences/import" element={<Navigate to="/hr/attendance/absences/import" replace />} />
          <Route path="employee-documents" element={<HrEmployeeDocumentsPage />} />
          <Route path="audit" element={<HrAuditPage />} />
          <Route path="master" element={<HrMasterPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="ACCOUNTING" />}>
        <Route path="/accounting/*" element={<LegacyAccountingRedirect />} />
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
          <Route path="salaries" element={<AccountingSalariesPage />} />
          <Route path="transportation" element={<AccountingTransportationPage />} />
          <Route path="collector-commissions" element={<AccountingCollectorCommissionsPage />} />
          <Route path="supervisor-commissions" element={<AccountingSupervisorCommissionsPage />} />
          <Route path="commission-rules" element={<AccountingCommissionRulesPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="DATA_ENTRY" />}>
        <Route path="/data-entry" element={<Navigate to="/data-entry/dashboard" replace />} />
        <Route path="/data-entry" element={<DataEntryRouteBoundary />}>
          <Route path="dashboard" element={<DataEntryDashboardPage />} />
          <Route path="clients" element={<DataEntryClientsPage />} />
          <Route path="clients/:id" element={<DataEntryClientDetailsPage />} />
          <Route path="import" element={<DataEntryImportPage />} />
          <Route path="history" element={<DataEntryHistoryPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="LEGAL" />}>
        <Route path="/legal" element={<Navigate to="/legal/dashboard" replace />} />
        <Route path="/legal" element={<LegalRouteBoundary />}>
          <Route path="dashboard" element={<LegalDashboardPage />} />
          <Route path="cases" element={<LegalCasesPage />} />
          <Route path="cases/:id" element={<LegalCaseDetailsPage />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route element={<ProtectedRoute department="COLLECTIONS" />}>
        <Route element={<CollectionsRouteBoundary />}>
          <Route path="/banks" element={<BanksPage />} />
          <Route path="/banks/:bankId" element={<BankClassificationPrimaryPage />} />
          <Route path="/banks/:bankId/:primary" element={<BankClassificationSecondaryPage />} />
          <Route path="/banks/:bankId/:primary/:secondary" element={<BankWorkspaceLayout />}>
            <Route index element={<Navigate to="overview" replace />} />
            <Route path="import" element={<BankPortfolioImportPage />} />
            <Route path="customers/import" element={<BankCustomerImportPage />} />
            <Route path="customers/:customerId" element={<BankCustomerDetailsPage />} />
            <Route path="customers" element={<BankCustomersPage />} />
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
          <Route path="/installment-companies" element={<InstallmentCompaniesPage />} />
          <Route path="/installment-companies/:companyId" element={<InstallmentClassificationPrimaryPage />} />
          <Route path="/installment-companies/:companyId/:primary" element={<InstallmentClassificationSecondaryPage />} />
          <Route path="/installment-companies/:companyId/:primary/:secondary" element={<BankWorkspaceLayout />}>
            <Route index element={<Navigate to="overview" replace />} />
            <Route path="import" element={<BankPortfolioImportPage />} />
            <Route path="customers/import" element={<BankCustomerImportPage />} />
            <Route path="customers/:customerId" element={<BankCustomerDetailsPage />} />
            <Route path="customers" element={<BankCustomersPage />} />
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
        </Route>
        <Route path="/collections" element={<Navigate to="/collections/dashboard" replace />} />
        <Route path="/collections" element={<CollectionsRouteBoundary />}>
          <Route path="dashboard" element={<CollectionsDashboardPage />} />
          <Route path="creditors" element={<CollectionsCreditorsDashboardPage />} />
          <Route path="collectors" element={<CollectionsCollectorsDashboardPage />} />
          <Route path="installment-companies" element={<Navigate to="/installment-companies" replace />} />
          <Route path="clients" element={<Navigate to="/banks" replace />} />
          <Route path="clients/:id" element={<Navigate to="/banks" replace />} />
          <Route path="cases" element={<CollectionCasesPage />} />
          <Route path="cases/:id" element={<CollectionCaseDetailsPage />} />
          <Route path="promises" element={<CollectionPromisesPage />} />
          <Route path="payments" element={<CollectionPaymentsPage />} />
          <Route path="assignments" element={<CollectionAssignmentsPage />} />
          <Route path="visits" element={<CollectionVisitsPage />} />
          <Route path="complaints" element={<CollectionComplaintsPage />} />
          <Route path="audit" element={<CollectionAuditPage />} />
          <Route path="imports" element={<Navigate to="/banks" replace />} />
          <Route path="data-batches" element={<CollectionsDataBatchesPage />} />
          <Route path="settings" element={<CollectionsSettingsPage />} />
          <Route path="reports" element={<CollectionsReportsPage />} />
          <Route path="branding" element={<Navigate to="/collections/settings?tab=identity" replace />} />
          <Route path="profile" element={<AccountProfilePage />} />
        </Route>
      </Route>
      <Route path="/unauthorized" element={<UnauthorizedPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
