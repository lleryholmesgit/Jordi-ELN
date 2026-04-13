using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class RecordTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public string DefaultRichText { get; set; } = string.Empty;

    public string DefaultStructuredDataJson { get; set; } = "{}";

    public string DefaultTableJson { get; set; } = "{\"columns\":[],\"rows\":[]}";

    public string DefaultFlowchartJson { get; set; } = "{\"nodes\":[],\"edges\":[]}";

    public RecordStatus Status { get; set; } = RecordStatus.Draft;

    public string SubmittedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public string ReviewedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    [MaxLength(2000)]
    public string ReviewComment { get; set; } = string.Empty;
}
