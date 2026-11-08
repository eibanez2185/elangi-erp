using System.Text.Json;
using Elangi.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elangi.UnitTests.Errors;

public sealed class ProblemDetailsExceptionHandlerTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(413)]
    [InlineData(422)]
    [InlineData(500)]
    public async Task TryHandleAsync_WritesProblemDetailsWithoutInternalErrors(int statusCode)
    {
        var responseBody = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = responseBody;
        var handler = new ProblemDetailsExceptionHandler(
            NullLogger<ProblemDetailsExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new ApiException(statusCode, "Request Error", "Safe client detail."),
            CancellationToken.None);

        responseBody.Position = 0;
        using var problem = await JsonDocument.ParseAsync(responseBody);

        Assert.True(handled);
        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("Request Error", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("Safe client detail.", problem.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("Exception", problem.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_HidesUnexpectedExceptionDetails()
    {
        var responseBody = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = responseBody;
        var handler = new ProblemDetailsExceptionHandler(
            NullLogger<ProblemDetailsExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            context,
            new InvalidOperationException("private implementation detail"),
            CancellationToken.None);

        responseBody.Position = 0;
        using var problem = await JsonDocument.ParseAsync(responseBody);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("An unexpected error occurred.", problem.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("private implementation detail", problem.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
