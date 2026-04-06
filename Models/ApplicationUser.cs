using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ElectronicLabNotebook.Models;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Department { get; set; } = string.Empty;
}