using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class ExperimentAttachment
{
    public int Id { get; set; }

    public int ExperimentRecordId { get; set; }

    public ExperimentRecord? ExperimentRecord { get; set; }

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string StoredFileName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long Length { get; set; }

    public bool IsImage { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string UploadedByUserId { get; set; } = string.Empty;
}