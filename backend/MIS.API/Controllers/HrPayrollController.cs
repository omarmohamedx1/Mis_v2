using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/payroll")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrPayrollController(IHrNetSalaryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSheet([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken) =>
        Ok(await service.GetSheetAsync(year, month, cancellationToken));
}
