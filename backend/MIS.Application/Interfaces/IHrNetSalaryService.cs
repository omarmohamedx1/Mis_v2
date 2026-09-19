using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IHrNetSalaryService
{
    Task<HrPayrollSheetDto> GetSheetAsync(int year, int month, CancellationToken cancellationToken);
}
