using MIS.Application.DTOs.Collections;

namespace MIS.Application.Interfaces;

public interface IBankPortfolioImportService
{
    Task<BankPortfolioImportDto> UploadAsync(Guid bankId, string fileName, string contentType, long length, Stream content, CancellationToken token);
    Task<BankPortfolioImportDataPreviewDto> PreviewDataAsync(Guid bankId, Guid importId, CancellationToken token);
    Task<BankPortfolioImportConfirmResultDto> ConfirmAsync(Guid bankId, Guid importId, string? notes, CancellationToken token);
    Task<BankPortfolioImportDto> UpdateNotesAsync(Guid bankId, Guid importId, string? notes, CancellationToken token);
    Task<BankPortfolioReplacementPreviewDto> PreviewReplacementAsync(Guid bankId, Guid importId, string fileName, string contentType, long length, Stream content, CancellationToken token);
    Task<BankPortfolioImportDto> ConfirmReplacementAsync(Guid bankId, Guid importId, string replacementToken, CancellationToken token);
    Task DeleteAsync(Guid bankId, Guid importId, CancellationToken token);
    Task<BankPortfolioImportDto> GetAsync(Guid bankId, Guid importId, CancellationToken token);
    Task<BankPortfolioImportPageDto> GetHistoryAsync(Guid bankId, int page, int pageSize, string? search, CancellationToken token);
}
