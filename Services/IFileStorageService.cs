using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.Services;

public interface IFileStorageService
{
    Task<ExperimentAttachment> SaveAsync(int recordId, IFormFile file, string uploadedByUserId, CancellationToken cancellationToken = default);
    Task<StoredFileResult?> GetAsync(ExperimentAttachment attachment, CancellationToken cancellationToken = default);
    Task DeleteAsync(ExperimentAttachment attachment, CancellationToken cancellationToken = default);
}

public sealed record StoredFileResult(Stream Content, string ContentType, string FileName);