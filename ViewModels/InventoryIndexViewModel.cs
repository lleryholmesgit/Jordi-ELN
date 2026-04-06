namespace ElectronicLabNotebook.ViewModels;

public sealed class InventoryIndexViewModel
{
    public required IReadOnlyList<ElectronicLabNotebook.Models.Instrument> Items { get; init; }
    public required IReadOnlyList<InventoryColumnPreference> Columns { get; init; }
}

public sealed class InventoryColumnPreference
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public int Position { get; init; }
    public bool IsVisible { get; init; }
}

public sealed class InventoryColumnPreferenceInput
{
    public string Key { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsVisible { get; set; }
}
