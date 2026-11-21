namespace Elangi.Api.Authentication;

public sealed record SupabaseJwtSettings(
    string Issuer,
    string Audience,
    string SigningSecret,
    bool UseJwks = false)
{
    public static SupabaseJwtSettings FromConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["SUPABASE_JWT_ISSUER"];
        var audience = configuration["SUPABASE_JWT_AUDIENCE"] ?? "authenticated";
        var signingSecret = configuration["SUPABASE_JWT_SECRET"];
        var useJwksValue = configuration["SUPABASE_JWT_USE_JWKS"];

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
        {
            throw new InvalidOperationException(
                "SUPABASE_JWT_ISSUER must be configured with the Supabase Auth issuer URL.");
        }

        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            throw new InvalidOperationException(
                "SUPABASE_JWT_SECRET must be configured outside source control.");
        }

        if (useJwksValue is not null && !bool.TryParse(useJwksValue, out _))
        {
            throw new InvalidOperationException("SUPABASE_JWT_USE_JWKS must be true or false.");
        }

        var useJwks = bool.TryParse(useJwksValue, out var configuredUseJwks) &&
                      configuredUseJwks;
        if (useJwks && issuerUri.Scheme != Uri.UriSchemeHttps && !issuerUri.IsLoopback)
        {
            throw new InvalidOperationException(
                "SUPABASE_JWT_ISSUER must use HTTPS when JWKS validation is enabled, except for loopback development.");
        }

        return new SupabaseJwtSettings(issuer, audience, signingSecret, useJwks);
    }
}
