using Elangi.Modules.Platform.Application;
using Elangi.Modules.Platform.Domain;
using Elangi.Modules.Security.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Elangi.Api.Errors;

public sealed class ProblemDetailsExceptionHandler(
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is CompanyValidationException validationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await ProblemDetailsResponseWriter.WriteAsync(
                httpContext.Response,
                new ValidationProblemDetails(validationException.Errors)
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Datos no válidos",
                    Detail = "Corrige los campos indicados e inténtalo nuevamente.",
                    Instance = httpContext.Request.Path
                },
                cancellationToken);

            return true;
        }

        var (status, title, detail) = exception switch
        {
            ApiException apiException => (apiException.StatusCode, apiException.Title, apiException.SafeDetail),
            CompanyAccessDeniedException => (StatusCodes.Status403Forbidden, "Forbidden", "The requested company operation is not authorized."),
            PlatformAccessDeniedException => (StatusCodes.Status403Forbidden, "Forbidden", "The requested platform operation is not authorized."),
            PlatformResourceNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "The requested resource was not found."),
            PlatformConflictException => (StatusCodes.Status409Conflict, "Conflict", "The request conflicts with the current resource state."),
            ArgumentException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable Content", "The request contains invalid values."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => (StatusCodes.Status409Conflict, "Conflict", "The request conflicts with an existing resource."),
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
