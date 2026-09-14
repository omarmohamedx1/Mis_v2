namespace MIS.Application.DTOs.Hr;

public sealed record SaveSocialInsuranceRequest(Guid EmployeeId, string SocialInsuranceNumber, DateOnly? InsuranceStartDate, decimal InsurableSalary, string InsuranceStatus, string? InsuranceOffice, string? ReferenceNumber, string? Notes);
public sealed record EndSocialInsuranceRequest(DateOnly InsuranceEndDate);
public sealed record SocialInsuranceDto(Guid Id, Guid EmployeeId, string SocialInsuranceNumber, DateOnly? InsuranceStartDate, DateOnly? InsuranceEndDate, decimal InsurableSalary, string InsuranceStatus, string? InsuranceOffice, string? ReferenceNumber, string? Notes);
public sealed record SocialInsuranceEmployeeDto(Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid DepartmentId, string Department, string? Position, string? NationalId, SocialInsuranceDto? Record);
public sealed record SocialInsurancePageDto(IReadOnlyCollection<SocialInsuranceEmployeeDto> Items, int TotalCount, int Page, int PageSize, int TotalPages, int TotalInsured, int NotInsured, int EndedRecords, bool CanManage);
