using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class ApplicationSetting
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}
