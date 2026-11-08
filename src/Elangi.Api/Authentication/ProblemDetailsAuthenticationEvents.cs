using Microsoft.AspNetCore.Authentication.JwtBearer;
using Elangi.Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Elangi.Api.Authentication;

public static class ProblemDetailsAuthenticationEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            await WriteAsync(context.Response, StatusCodes.Status401Unauthorized, "Unauthorized");
        },
        OnForbidden = async context =>
            await WriteAsync(context.Response, StatusCodes.Status403Forbidden, "Forbidden")
    };

    private static async Task WriteAsync(HttpResponse response, int statusCode, string title)
    {
        response.StatusCode = statusCode;
        await ProblemDetailsResponseWriter.WriteAsync(
            response,
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Type = $"https://httpstatuses.com/{statusCode}"
            },
            response.HttpContext.RequestAborted);
    }
}
