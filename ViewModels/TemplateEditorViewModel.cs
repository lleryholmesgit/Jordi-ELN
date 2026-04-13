using System.ComponentModel.DataAnnotations;
using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.ViewModels;

public sealed class TemplateEditorViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public string DefaultRichText { get; set; } = "<p><br></p>";
    public RecordStatus Status { get; set; } = RecordStatus.Draft;
    public string ReviewComment { get; set; } = string.Empty;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public List<RecordInventoryLookupOptionViewModel> InventoryLookupOptions { get; set; } = new();
    public List<RecordNotebookLookupOptionViewModel> NotebookLookupOptions { get; set; } = new();
}

public sealed class TemplateSummaryViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string PreviewHtml { get; init; } = string.Empty;
    public bool HasHighlights { get; init; }
    public RecordStatus Status { get; init; }
}

public sealed class RecordTemplatePayloadViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DefaultRichText { get; init; } = string.Empty;
}
