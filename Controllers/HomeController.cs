using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicLabNotebook.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Inventory");
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
