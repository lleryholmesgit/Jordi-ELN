using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class ExperimentRecord
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string ExperimentCode { get; set; } = string.Empty;

    public DateOnly ConductedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [MaxLength(160)]
    public string ProjectName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string PrincipalInvestigator { get; set; } = string.Empty;

    public int? TemplateId { get; set; }

    public RecordTemplate? Template { get; set; }

    public string RichTextContent { get; set; } = string.Empty;

    public string StructuredDataJson { get; set; } = "{}";

    public string TableJson { get; set; } = "{\"columns\":[],\"rows\":[]}";

    public string FlowchartJson { get; set; } = "{\"nodes\":[],\"edges\":[]}";

    public string FlowchartPreviewPath { get; set; } = string.Empty;

    public RecordStatus Status { get; set; } = RecordStatus.Draft;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public string LastUpdatedByUserId { get; set; } = string.Empty;

    public string SubmittedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public string ReviewedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    [MaxLength(2000)]
    public string ReviewComment { get; set; } = string.Empty;

    [MaxLength(256)]
    public string SignatureStatement { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SignatureReason { get; set; } = string.Empty;

    public DateTimeOffset? SignatureTimestampUtc { get; set; }

    [MaxLength(128)]
    public string SignatureHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RecordInstrumentLink> InstrumentLinks { get; set; } = new List<RecordInstrumentLink>();

    public ICollection<ExperimentAttachment> Attachments { get; set; } = new List<ExperimentAttachment>();

    public ICollection<ReviewAction> ReviewHistory { get; set; } = new List<ReviewAction>();
}