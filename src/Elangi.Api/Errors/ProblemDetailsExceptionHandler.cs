using Elangi.Modules.Security.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Elangi.Api.Errors;

public sealed class ProblemDetailsExceptionHandler(
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ApiException apiException => (apiException.StatusCode, apiException.Title, apiException.SafeDetail),
            CompanyAccessDeniedException => (StatusCodes.Status403Forbidden, "Forbidden", "The requested company operation is not authorized."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", "The requested operation is not authorized."),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "An unhandled request error occurred.");
        }

        httpContext.Response.StatusCode = status;
        await ProblemDetailsResponseWriter.WriteAsync(
            httpContext.Response,
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}"
            },
            cancellationToken);

        return true;
    }
}
