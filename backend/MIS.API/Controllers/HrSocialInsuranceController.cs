using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/social-insurance")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrSocialInsuranceController(ISocialInsuranceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, string? search = null, Guid? departmentId = null, string? status = null, Guid? employeeId = null, int page = 1, int pageSize = 20)
        => Ok(await service.ListAsync(search, departmentId, status, employeeId, page, pageSize, ct));
    [HttpGet("employees/{employeeId:guid}")]
    public async Task<IActionResult> History(Guid employeeId, CancellationToken ct) => Ok(await service.HistoryAsync(employeeId, ct));
    [HttpPost]
    public async Task<IActionResult> Create(SaveSocialInsuranceRequest request, CancellationToken ct) => Ok(await service.SaveAsync(null, request, ct));
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, SaveSocialInsuranceRequest request, CancellationToken ct) => Ok(await service.SaveAsync(id, request, ct));
    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, EndSocialInsuranceRequest request, CancellationToken ct) => Ok(await service.EndAsync(id, request, ct));
}
