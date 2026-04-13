namespace ElectronicLabNotebook.Services;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Researcher = "Researcher";
    public const string Reviewer = "Reviewer";

    public static readonly string[] ManagedRoles = { Admin, Researcher, Reviewer };

    public static bool HasAssignedRole(IEnumerable<string> roles)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ManagedRoles.Any(roleSet.Contains);
    }
}
