using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.Models;

public sealed class Instrument
{
    public int Id { get; set; }

    public InventoryItemType ItemType { get; set; } = InventoryItemType.Instrument;

    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Manufacturer { get; set; } = string.Empty;

    [MaxLength(160)]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Location { get; set; } = string.Empty;

    public int? StorageLocationId { get; set; }

    public StorageLocation? StorageLocation { get; set; }

    public InstrumentStatus Status { get; set; } = InstrumentStatus.Active;

    [MaxLength(160)]
    public string OwnerName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string CalibrationInfo { get; set; } = string.Empty;

    [MaxLength(160)]
    public string ProductNumber { get; set; } = string.Empty;

    [MaxLength(160)]
    public string CatalogNumber { get; set; } = string.Empty;

    [MaxLength(160)]
    public string LotNumber { get; set; } = string.Empty;

    [MaxLength(160)]
    public string ExpNumber { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    [MaxLength(32)]
    public string Unit { get; set; } = string.Empty;

    public DateOnly? OpenedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string QrCodeToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RecordInstrumentLink> RecordLinks { get; set; } = new List<RecordInstrumentLink>();
}
