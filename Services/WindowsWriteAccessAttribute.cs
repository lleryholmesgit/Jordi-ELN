using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ElectronicLabNotebook.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class WindowsWriteAccessAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!ClientDeviceDetector.IsPhoneClient(context.HttpContext.Request))
        {
            base.OnActionExecuting(context);
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData["AccessDeniedMessage"] = "iPhone clients are limited to scan and read-only mobile workflows. Use iPad or Windows for full editing and administrative actions.";
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            return;
        }

        context.Result = new ForbidResult();
    }
}
