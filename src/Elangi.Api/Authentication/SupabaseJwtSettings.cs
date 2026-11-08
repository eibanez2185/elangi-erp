namespace Elangi.Api.Authentication;

public sealed record SupabaseJwtSettings(string Issuer, string Audience, string SigningSecret)
{
    public static SupabaseJwtSettings FromConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["SUPABASE_JWT_ISSUER"];
        var audience = configuration["SUPABASE_JWT_AUDIENCE"] ?? "authenticated";
        var signingSecret = configuration["SUPABASE_JWT_SECRET"];

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "SUPABASE_JWT_ISSUER must be configured with the Supabase Auth issuer URL.");
        }

        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new InvalidOperationException(
                "SUPABASE_JWT_SECRET must be configured outside source control.");
        }

        return new SupabaseJwtSettings(issuer, audience, signingSecret);
    }
}
