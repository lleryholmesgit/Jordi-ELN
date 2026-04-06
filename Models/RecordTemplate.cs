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
}