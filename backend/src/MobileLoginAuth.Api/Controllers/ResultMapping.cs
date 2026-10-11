using Microsoft.AspNetCore.Mvc;
using MobileLoginAuth.Application.Common;

namespace MobileLoginAuth.Api.Controllers;

public static class ResultMapping
{
    public static IActionResult ToProblem(this ControllerBase controller, Result result)
    {
        var status = result.ErrorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Locked => StatusCodes.Status423Locked,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        return controller.Problem(detail: result.Error, statusCode: status);
    }

    public static IActionResult ValidationProblem(this ControllerBase controller, FluentValidation.Results.ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return controller.ValidationProblem(controller.ModelState);
    }
}
