using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ElectronicLabNotebook.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class WindowsWriteAccessAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!ClientDeviceDetector.IsIosClient(context.HttpContext.Request))
        {
            base.OnActionExecuting(context);
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData["AccessDeniedMessage"] = "iOS clients are currently read-only. Please use a Windows device for editing and administrative actions.";
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            return;
        }

        context.Result = new ForbidResult();
    }
}
