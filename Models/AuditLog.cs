using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class AuditLog
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string ActionType { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string EntityType { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SourceClient { get; set; } = string.Empty;

    public string BeforeJson { get; set; } = string.Empty;

    public string AfterJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}