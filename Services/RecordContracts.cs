using System.ComponentModel.DataAnnotations;
using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.Services;

public sealed class RecordSaveRequest
{
    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ExperimentCode { get; set; } = string.Empty;

    public DateOnly ConductedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public string? ProjectName { get; set; }

    public string? PrincipalInvestigator { get; set; }

    public int? TemplateId { get; set; }

    public string? RichTextContent { get; set; }

    public string? StructuredDataJson { get; set; } = "{}";

    public string? TableJson { get; set; } = "{\"columns\":[],\"rows\":[]}";

    public string? FlowchartJson { get; set; } = "{\"nodes\":[],\"edges\":[]}";

    public string? FlowchartPreviewPath { get; set; }

    public string? SignatureStatement { get; set; }

    public DateOnly? SignatureDate { get; set; }

    public List<RecordInstrumentLinkRequest> InstrumentLinks { get; set; } = new();
}

public sealed class RecordInstrumentLinkRequest
{
    public int InstrumentId { get; set; }

    public decimal? UsageHours { get; set; }
}

public sealed class RecordReviewRequest
{
    public string? Comment { get; set; }
}

public sealed class RecordSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public RecordStatus? Status { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SortBy { get; set; } = "UpdatedAtUtc";
    public bool Descending { get; set; } = true;
}
