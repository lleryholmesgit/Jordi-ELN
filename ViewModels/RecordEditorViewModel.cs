using System.ComponentModel.DataAnnotations;
using ElectronicLabNotebook.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectronicLabNotebook.ViewModels;

public sealed class RecordEditorViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string ExperimentCode { get; set; } = string.Empty;

    [Display(Name = "Date")]
    [DataType(DataType.Date)]
    public DateOnly ConductedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Project Name")]
    public string? ProjectName { get; set; }

    [Display(Name = "Technician")]
    public string? PrincipalInvestigator { get; set; }
    public int? TemplateId { get; set; }
    public string? RichTextContent { get; set; }
    public string? NotebookBlocksJson { get; set; } = "[]";
    public string? FlowchartJson { get; set; } = "{\"nodes\":[],\"edges\":[]}";
    public string? FlowchartPreviewPath { get; set; }
    public string? SignatureStatement { get; set; } = "I confirm this e-signature reflects my intent.";
    public string? InstrumentLinksJson { get; set; } = "[]";
    public RecordStatus Status { get; set; } = RecordStatus.Draft;
    public string? ReviewComment { get; set; }
    public List<SelectListItem> TemplateOptions { get; set; } = new();
    public List<SelectListItem> InstrumentOptions { get; set; } = new();
    public List<RecordInventoryLookupOptionViewModel> InventoryLookupOptions { get; set; } = new();
    public List<RecordNotebookLookupOptionViewModel> NotebookLookupOptions { get; set; } = new();
}

public sealed class RecordInventoryLookupOptionViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DetailPath { get; set; } = string.Empty;
}

public sealed class RecordNotebookLookupOptionViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DetailPath { get; set; } = string.Empty;
}
