namespace Elangi.Modules.Platform.Domain;

public sealed class CompanyValidationException(string field, string message)
    : ArgumentException(message, field)
{
    public Dictionary<string, string[]> Errors { get; } =
        new() { [field] = [message] };
}
