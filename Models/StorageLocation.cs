using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class StorageLocation
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string QrCodeToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Instrument> InventoryItems { get; set; } = new List<Instrument>();
}
