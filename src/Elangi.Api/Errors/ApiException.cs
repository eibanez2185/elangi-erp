namespace Elangi.Api.Errors;

public sealed class ApiException(int statusCode, string title, string safeDetail) : Exception(safeDetail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string SafeDetail { get; } = safeDetail;
}
