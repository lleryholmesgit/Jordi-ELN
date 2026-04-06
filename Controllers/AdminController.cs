using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _context.Users.OrderBy(x => x.Email).ToListAsync(cancellationToken);
        var userViewModels = new List<AdminUserRoleViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userViewModels.Add(new AdminUserRoleViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                IsAdmin = roles.Contains(Roles.Admin),
                IsResearcher = roles.Contains(Roles.Researcher),
                IsReviewer = roles.Contains(Roles.Reviewer)
            });
        }

        var model = new AdminIndexViewModel
        {
            Users = userViewModels,
            Templates = await _context.RecordTemplates
                .OrderBy(x => x.Name)
                .Select(x => new RecordTemplateSummaryViewModel
                {
                    Name = x.Name,
                    Description = x.Description
                })
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoles(string userId, bool isAdmin, bool isResearcher, bool isReviewer)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["AdminError"] = "The selected user could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var desiredRoles = new List<string>();
        if (isAdmin) desiredRoles.Add(Roles.Admin);
        if (isResearcher) desiredRoles.Add(Roles.Researcher);
        if (isReviewer) desiredRoles.Add(Roles.Reviewer);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToAdd = desiredRoles.Except(currentRoles).ToArray();
        var rolesToRemove = currentRoles.Except(desiredRoles).ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                TempData["AdminError"] = string.Join(" ", removeResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        if (rolesToAdd.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                TempData["AdminError"] = string.Join(" ", addResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        TempData["AdminMessage"] = $"Updated roles for {user.Email}.";
        return RedirectToAction(nameof(Index));
    }
}
