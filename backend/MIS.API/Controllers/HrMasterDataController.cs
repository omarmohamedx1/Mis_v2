using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MIS.API.Authorization;
using MIS.Application.Common;
using MIS.Application.DTOs.Hr;
using MIS.Application.Interfaces;

namespace MIS.API.Controllers;

[ApiController]
[Route("api/hr/master")]
[Authorize(Policy = AuthorizationPolicies.HrDepartment)]
public sealed class HrMasterDataController : ControllerBase
{
    private readonly IHrMasterDataService _service;

    public HrMasterDataController(IHrMasterDataService service)
    {
        _service = service;
    }

    [HttpGet("categories")]
    public IActionResult GetCategories() => Ok(HrMasterDataCategories.All);

    [HttpGet("{category}")]
    public async Task<ActionResult<PagedMasterDataDto>> GetPaged(
        string category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(page, pageSize, search);
        return Ok(await _service.GetPagedAsync(category, page, pageSize, search, isActive, cancellationToken));
    }

    [HttpGet("{category}/lookup")]
    public async Task<ActionResult<IReadOnlyCollection<MasterDataLookupDto>>> GetLookup(
        string category,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetLookupAsync(category, includeInactive, cancellationToken));
    }

    [HttpGet("{category}/{id:guid}")]
    public async Task<ActionResult<MasterDataItemDto>> GetById(string category, Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetByIdAsync(category, id, cancellationToken));
    }

    [HttpPost("{category}")]
    public async Task<ActionResult<MasterDataItemDto>> Create(
        string category,
        SaveMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(category, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { category, id = created.Id }, created);
    }

    [HttpPut("{category}/{id:guid}")]
    public async Task<ActionResult<MasterDataItemDto>> Update(
        string category,
        Guid id,
        SaveMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateAsync(category, id, request, cancellationToken));
    }

    [HttpPatch("{category}/{id:guid}/active")]
    public async Task<ActionResult<MasterDataItemDto>> SetActive(
        string category,
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.SetActiveAsync(category, id, request.IsActive, cancellationToken));
    }

    [HttpDelete("{category}/{id:guid}")]
    public async Task<IActionResult> Delete(string category, Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(category, id, cancellationToken);
        return NoContent();
    }

    private static void ValidatePaging(int page, int pageSize, string? search)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new HrValidationException("Page must be at least 1 and pageSize must be between 1 and 100.");
        }

        if (search?.Length > 160)
        {
            throw new HrValidationException("Search cannot exceed 160 characters.");
        }
    }
}
