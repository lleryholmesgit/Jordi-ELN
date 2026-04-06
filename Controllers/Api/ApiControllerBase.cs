using ElectronicLabNotebook.Models;
using Microsoft.AspNetCore.Identity;

namespace ElectronicLabNotebook.Controllers.Api;

public abstract class ApiControllerBase : Microsoft.AspNetCore.Mvc.ControllerBase
{
    protected string GetActorUserId(UserManager<ApplicationUser> userManager)
    {
        return userManager.GetUserId(User) ?? string.Empty;
    }
}