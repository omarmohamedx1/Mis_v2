namespace MIS.Application.DTOs.Hr;

public sealed record DepartmentEmployeeCountDto(
    Guid DepartmentId,
    string DepartmentName,
    string DepartmentCode,
    int EmployeeCount);

public sealed record BranchEmployeeCountDto(
    Guid? BranchId,
    string BranchName,
    string? BranchCode,
    int EmployeeCount);

public sealed record OrganizationEmployeeCountDto(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationCode,
    int EmployeeCount);

public sealed record TodayAttendanceSummaryDto(
    int Present,
    int Absent,
    int Late,
    int OnLeave,
    int MissingCheckOut);

public sealed record HrDashboardAlertDto(
    string Category,
    Guid EntityId,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string Title,
    DateOnly DueDate,
    int DaysRemaining,
    string Severity);

public sealed record HrDashboardActivityDto(
    Guid Id,
    string Action,
    string Message,
    string Username,
    Guid? EmployeeId,
    string? EmployeeName,
    DateTimeOffset Timestamp);

public sealed record AttendanceTrendPointDto(
    DateOnly Date,
    int Present,
    int Late,
    int Absent,
    int OnLeave);

public sealed record AbsenceTrendPointDto(DateOnly Date, int Absences);

public sealed record HrDashboardDto(
    int TotalEmployees,
    int ActiveEmployees,
    int? AbsentToday,
    bool AttendanceAvailable,
    int? DocumentsRequiringAttention,
    bool DocumentAttentionAvailable,
    int TotalDocuments,
    IReadOnlyCollection<DepartmentEmployeeCountDto> EmployeesByDepartment,
    IReadOnlyCollection<OrganizationEmployeeCountDto> EmployeesByOrganization,
    int InactiveEmployees,
    IReadOnlyCollection<BranchEmployeeCountDto> EmployeesByBranch,
    TodayAttendanceSummaryDto TodayAttendance,
    IReadOnlyCollection<HrDashboardAlertDto> Alerts,
    IReadOnlyCollection<HrDashboardActivityDto> RecentActivity,
    IReadOnlyCollection<AttendanceTrendPointDto> AttendanceTrend,
    IReadOnlyCollection<AbsenceTrendPointDto> AbsenceTrend);
