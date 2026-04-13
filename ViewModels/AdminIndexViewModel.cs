namespace ElectronicLabNotebook.ViewModels;

public sealed class AdminIndexViewModel
{
    public required IReadOnlyList<AdminUserRoleViewModel> Users { get; init; }
    public required IReadOnlyList<RecordTemplateSummaryViewModel> Templates { get; init; }
}

public sealed class AdminUserRoleViewModel
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public string Department { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool IsResearcher { get; init; }
    public bool IsReviewer { get; init; }
    public bool HasAssignedRole { get; init; }
    public bool CanDelete { get; init; }
}

public sealed class RecordTemplateSummaryViewModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
}
