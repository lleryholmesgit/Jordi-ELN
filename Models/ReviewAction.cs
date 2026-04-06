using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class ReviewAction
{
    public int Id { get; set; }

    public int ExperimentRecordId { get; set; }

    public ExperimentRecord? ExperimentRecord { get; set; }

    [Required, MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}