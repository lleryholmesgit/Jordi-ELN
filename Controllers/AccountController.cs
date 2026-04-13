using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace ElectronicLabNotebook.Controllers;

public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl ?? string.Empty });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var assignedRoles = await _userManager.GetRolesAsync(user);
        if (!Roles.HasAssignedRole(assignedRoles))
        {
            ModelState.AddModelError(string.Empty, "Your account has been registered and is waiting for an admin to assign access.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Email = model.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName.Trim(),
            Department = model.Department?.Trim() ?? string.Empty,
            EmailConfirmed = true,
            RecoveryQuestion = model.RecoveryQuestion.Trim()
        };
        user.RecoveryAnswerHash = _passwordHasher.HashPassword(user, NormalizeRecoveryAnswer(model.RecoveryAnswer));

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["AccountMessage"] = "Account registered. An admin must assign your access level before you can sign in.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> RecoveryQuestion(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Json(new { question = string.Empty });
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        return Json(new
        {
            question = user?.RecoveryQuestion ?? string.Empty
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        var user = string.IsNullOrWhiteSpace(model.Email)
            ? null
            : await _userManager.FindByEmailAsync(model.Email.Trim());

        if (user is not null)
        {
            model.RecoveryQuestion = user.RecoveryQuestion;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (user is null || string.IsNullOrWhiteSpace(user.RecoveryAnswerHash))
        {
            ModelState.AddModelError(nameof(model.Email), "We could not verify the recovery details for this email address.");
            return View(model);
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.RecoveryAnswerHash,
            NormalizeRecoveryAnswer(model.RecoveryAnswer));

        if (verification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.RecoveryAnswer), "The recovery answer does not match our records.");
            return View(model);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.RecoveryAnswerHash = _passwordHasher.HashPassword(user, NormalizeRecoveryAnswer(model.RecoveryAnswer));
            await _userManager.UpdateAsync(user);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["AccountMessage"] = "Your password has been reset. You can now sign in with the new password.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["AccountMessage"] = "The password reset link is invalid or incomplete.";
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            TempData["AccountMessage"] = "Your password has been reset. You can now sign in with the new password.";
            return RedirectToAction(nameof(Login));
        }

        string decodedToken;
        try
        {
            decodedToken = DecodeResetToken(model.Token);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "The password reset link is invalid.");
            return View(model);
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);
        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["AccountMessage"] = "Your password has been reset. You can now sign in with the new password.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private static string NormalizeRecoveryAnswer(string answer)
    {
        return string.Join(' ', answer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private static string DecodeResetToken(string encodedToken)
    {
        return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
    }
}
