using System.ComponentModel.DataAnnotations;
using ElectronicLabNotebook.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectronicLabNotebook.ViewModels;

public sealed class InstrumentEditorViewModel
{
    public int? Id { get; set; }

    public InventoryItemType ItemType { get; set; } = InventoryItemType.Instrument;

    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; } = string.Empty;
    public string? Manufacturer { get; set; } = string.Empty;
    public string? SerialNumber { get; set; } = string.Empty;
    public string? Location { get; set; } = string.Empty;
    [Display(Name = "Storage location")]
    public int? StorageLocationId { get; set; }
    public InstrumentStatus Status { get; set; } = InstrumentStatus.Active;
    public string? OwnerName { get; set; } = string.Empty;
    public string? CalibrationInfo { get; set; } = string.Empty;
    public string? ProductNumber { get; set; } = string.Empty;
    public string? CatalogNumber { get; set; } = string.Empty;
    public string? LotNumber { get; set; } = string.Empty;
    public string? ExpNumber { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; } = string.Empty;
    [DataType(DataType.Date)]
    public DateOnly? OpenedOn { get; set; }
    [DataType(DataType.Date)]
    public DateOnly? ExpiresOn { get; set; }
    public string? Notes { get; set; } = string.Empty;
    public string? QrCodeToken { get; set; } = string.Empty;
    public string? QrCodeSvg { get; set; } = string.Empty;
    public List<SelectListItem> StorageLocationOptions { get; set; } = new();
}
