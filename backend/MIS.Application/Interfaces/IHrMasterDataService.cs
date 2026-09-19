using MIS.Application.DTOs.Hr;

namespace MIS.Application.Interfaces;

public interface IHrMasterDataService
{
    Task<PagedMasterDataDto> GetPagedAsync(string category, int page, int pageSize, string? search, bool? isActive, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MasterDataLookupDto>> GetLookupAsync(string category, bool includeInactive, CancellationToken cancellationToken);

    Task<MasterDataItemDto> GetByIdAsync(string category, Guid id, CancellationToken cancellationToken);

    Task<MasterDataItemDto> CreateAsync(string category, SaveMasterDataRequest request, CancellationToken cancellationToken);

    Task<MasterDataItemDto> UpdateAsync(string category, Guid id, SaveMasterDataRequest request, CancellationToken cancellationToken);

    Task<MasterDataItemDto> SetActiveAsync(string category, Guid id, bool isActive, CancellationToken cancellationToken);

    Task DeleteAsync(string category, Guid id, CancellationToken cancellationToken);
}
