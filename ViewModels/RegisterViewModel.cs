using System.ComponentModel.DataAnnotations;

namespace ElectronicLabNotebook.ViewModels;

public sealed class RegisterViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Department { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [Display(Name = "Recovery question")]
    public string RecoveryQuestion { get; set; } = "What is your lab or department name?";

    [Required, MaxLength(200)]
    [DataType(DataType.Password)]
    [Display(Name = "Recovery answer")]
    public string RecoveryAnswer { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
