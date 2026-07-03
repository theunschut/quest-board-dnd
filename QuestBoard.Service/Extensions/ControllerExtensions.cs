using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Extensions;

/// <summary>
/// Reusable MVC-layer boilerplate helpers for controller actions: the
/// TempData-message-then-redirect pattern and the ModelState-invalid guard pattern.
/// </summary>
internal static class ControllerExtensions
{
    /// <summary>
    /// Sets a TempData entry and redirects to the given action within the same controller.
    /// The action name is always supplied by the caller — never hard-coded here.
    /// </summary>
    internal static IActionResult RedirectWithMessage(this Controller controller, string action, string tempDataKey, string message)
    {
        controller.TempData[tempDataKey] = message;
        return controller.RedirectToAction(action);
    }

    /// <summary>
    /// Sets TempData["Success"] and redirects to the given action.
    /// </summary>
    internal static IActionResult RedirectWithSuccess(this Controller controller, string action, string message)
        => controller.RedirectWithMessage(action, "Success", message);

    /// <summary>
    /// Sets TempData["Error"] and redirects to the given action.
    /// </summary>
    internal static IActionResult RedirectWithError(this Controller controller, string action, string message)
        => controller.RedirectWithMessage(action, "Error", message);

    /// <summary>
    /// Sets TempData["Warning"] and redirects to the given action.
    /// </summary>
    internal static IActionResult RedirectWithWarning(this Controller controller, string action, string message)
        => controller.RedirectWithMessage(action, "Warning", message);

    /// <summary>
    /// Collapses the "if (!ModelState.IsValid) return View(model);" shape into a single call.
    /// Returns true and sets <paramref name="result"/> to the invalid-model view when the
    /// model state is invalid; otherwise returns false and leaves <paramref name="result"/> null.
    /// </summary>
    internal static bool TryReturnInvalidModel(this Controller controller, object model, out IActionResult? result)
    {
        if (!controller.ModelState.IsValid)
        {
            result = controller.View(model);
            return true;
        }

        result = null;
        return false;
    }
}
