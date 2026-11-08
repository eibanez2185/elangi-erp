using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Elangi.Api.Errors;

public static class ProblemDetailsResponseWriter
{
    public static Task WriteAsync(
        HttpResponse response,
        ProblemDetails problemDetails,
        CancellationToken cancellationToken)
    {
        response.ContentType = "application/problem+json";
        return JsonSerializer.SerializeAsync(
            response.Body,
            problemDetails,
            cancellationToken: cancellationToken);
    }
}
