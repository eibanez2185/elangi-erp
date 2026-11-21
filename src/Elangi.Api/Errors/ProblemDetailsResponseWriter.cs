using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Elangi.Api.Errors;

public static class ProblemDetailsResponseWriter
{
    public static Task WriteAsync<TProblemDetails>(
        HttpResponse response,
        TProblemDetails problemDetails,
        CancellationToken cancellationToken)
        where TProblemDetails : ProblemDetails
    {
        response.ContentType = "application/problem+json";
        return JsonSerializer.SerializeAsync(
            response.Body,
            problemDetails,
            cancellationToken: cancellationToken);
    }
}
