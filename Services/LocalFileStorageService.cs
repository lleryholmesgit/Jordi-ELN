using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public LocalFileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<ExperimentAttachment> SaveAsync(int recordId, IFormFile file, string uploadedByUserId, CancellationToken cancellationToken = default)
    {
        var root = GetRootPath();
        Directory.CreateDirectory(root);

        var storedFileName = $"{recordId}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(root, storedFileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return new ExperimentAttachment
        {
            ExperimentRecordId = recordId,
            FileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Length = file.Length,
            IsImage = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
            UploadedByUserId = uploadedByUserId
        };
    }

    public Task<StoredFileResult?> GetAsync(ExperimentAttachment attachment, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(GetRootPath(), attachment.StoredFileName);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredFileResult?>(null);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult<StoredFileResult?>(new StoredFileResult(stream, attachment.ContentType, attachment.FileName));
    }

    public Task DeleteAsync(ExperimentAttachment attachment, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(GetRootPath(), attachment.StoredFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetRootPath()
    {
        var configured = _configuration["FileStorage:RootPath"] ?? "App_Data/Uploads";
        return Path.Combine(_environment.ContentRootPath, configured);
    }
}