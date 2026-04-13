using System.Text;
using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountEmailService _accountEmailService;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAccountEmailService accountEmailService)
    {
        _context = context;
        _userManager = userManager;
        _accountEmailService = accountEmailService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _context.Users.OrderBy(x => x.Email).ToListAsync(cancellationToken);
        var userViewModels = new List<AdminUserRoleViewModel>();
        var currentUserId = _userManager.GetUserId(User);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userViewModels.Add(new AdminUserRoleViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                Department = user.Department,
                IsAdmin = roles.Contains(Roles.Admin),
                IsResearcher = roles.Contains(Roles.Researcher),
                IsReviewer = roles.Contains(Roles.Reviewer),
                HasAssignedRole = Roles.HasAssignedRole(roles),
                CanDelete = !roles.Contains(Roles.Admin) && !string.Equals(currentUserId, user.Id, StringComparison.Ordinal)
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
    [WindowsWriteAccess]
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

        if (desiredRoles.Count == 0)
        {
            TempData["AdminError"] = "At least one role must be assigned.";
            return RedirectToAction(nameof(Index));
        }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> DeleteUser(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["AdminError"] = "The selected user could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(currentUserId, user.Id, StringComparison.Ordinal))
        {
            TempData["AdminError"] = "The current admin account cannot delete itself.";
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin))
        {
            TempData["AdminError"] = "Admin accounts cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var archiveUser = await EnsureArchivedUserAsync();
        await ReassignUserReferencesAsync(user.Id, archiveUser.Id, cancellationToken);

        if (roles.Count > 0)
        {
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, roles);
            if (!removeRolesResult.Succeeded)
            {
                TempData["AdminError"] = string.Join(" ", removeRolesResult.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(Index));
            }
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            TempData["AdminError"] = string.Join(" ", deleteResult.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["AdminMessage"] = $"Deleted account {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> SendPasswordResetEmail(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            TempData["AdminError"] = "The selected user could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUrl = Url.Action("ResetPassword", "Account", new { email = user.Email, token = encodedToken }, Request.Scheme);
        if (string.IsNullOrWhiteSpace(resetUrl))
        {
            TempData["AdminError"] = "Unable to generate the password reset link.";
            return RedirectToAction(nameof(Index));
        }

        var summary = await _accountEmailService.SendPasswordResetEmailAsync(user.Email, resetUrl);
        TempData["AdminMessage"] = summary;
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser> EnsureArchivedUserAsync()
    {
        const string archivedEmail = "deleted-account@system.local";
        var archivedUser = await _context.Users.FirstOrDefaultAsync(x => x.Email == archivedEmail);
        if (archivedUser is not null)
        {
            return archivedUser;
        }

        archivedUser = new ApplicationUser
        {
            UserName = archivedEmail,
            Email = archivedEmail,
            EmailConfirmed = true,
            DisplayName = "Deleted Account Archive",
            Department = "System",
            RecoveryQuestion = "System archive",
            RecoveryAnswerHash = string.Empty
        };

        var createResult = await _userManager.CreateAsync(archivedUser);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create archived user placeholder: {string.Join(" ", createResult.Errors.Select(x => x.Description))}");
        }

        return archivedUser;
    }

    private async Task ReassignUserReferencesAsync(string sourceUserId, string replacementUserId, CancellationToken cancellationToken)
    {
        var records = await _context.ExperimentRecords
            .Where(x => x.CreatedByUserId == sourceUserId
                        || x.LastUpdatedByUserId == sourceUserId
                        || x.SubmittedByUserId == sourceUserId
                        || x.ReviewedByUserId == sourceUserId)
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            if (record.CreatedByUserId == sourceUserId) record.CreatedByUserId = replacementUserId;
            if (record.LastUpdatedByUserId == sourceUserId) record.LastUpdatedByUserId = replacementUserId;
            if (record.SubmittedByUserId == sourceUserId) record.SubmittedByUserId = replacementUserId;
            if (record.ReviewedByUserId == sourceUserId) record.ReviewedByUserId = replacementUserId;
        }

        var reviewActions = await _context.ReviewActions.Where(x => x.ActorUserId == sourceUserId).ToListAsync(cancellationToken);
        foreach (var action in reviewActions)
        {
            action.ActorUserId = replacementUserId;
        }

        var attachments = await _context.ExperimentAttachments.Where(x => x.UploadedByUserId == sourceUserId).ToListAsync(cancellationToken);
        foreach (var attachment in attachments)
        {
            attachment.UploadedByUserId = replacementUserId;
        }

        var links = await _context.RecordInstrumentLinks.Where(x => x.LinkedByUserId == sourceUserId).ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            link.LinkedByUserId = replacementUserId;
        }

        var auditLogs = await _context.AuditLogs.Where(x => x.ActorUserId == sourceUserId).ToListAsync(cancellationToken);
        foreach (var auditLog in auditLogs)
        {
            auditLog.ActorUserId = replacementUserId;
        }

        var settingsToRemove = await _context.ApplicationSettings
            .Where(x => x.Key == $"InventoryLayout:{sourceUserId}")
            .ToListAsync(cancellationToken);
        if (settingsToRemove.Count > 0)
        {
            _context.ApplicationSettings.RemoveRange(settingsToRemove);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
