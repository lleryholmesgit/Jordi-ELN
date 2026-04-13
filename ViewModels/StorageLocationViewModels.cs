using System.ComponentModel.DataAnnotations;
using ElectronicLabNotebook.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectronicLabNotebook.ViewModels;

public sealed class StorageLocationIndexViewModel
{
    public required IReadOnlyList<StorageLocationSummaryViewModel> Locations { get; init; }
}

public sealed class StorageLocationSummaryViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public int InventoryItemCount { get; init; }
}

public sealed class StorageLocationEditorViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public string QrCodeToken { get; set; } = string.Empty;
    public string QrCodeSvg { get; set; } = string.Empty;

    public List<Instrument> InventoryItems { get; set; } = new();
    public List<SelectListItem> AvailableInventoryOptions { get; set; } = new();
    public List<SelectListItem> ReplacementLocationOptions { get; set; } = new();
    public List<int> SelectedInventoryItemIds { get; set; } = new();
    public int? ReplacementStorageLocationId { get; set; }
}
