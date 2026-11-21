using Elangi.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Elangi.IntegrationTests.Authentication;

[Collection("Local Supabase")]
public sealed class SupabaseJwksConfigurationTests
{
    [Fact]
    public async Task LoadsTheLocalSupabaseEcdsaSigningKey()
    {
        var issuer = Environment.GetEnvironmentVariable("SUPABASE_JWT_ISSUER");
        var signingSecret = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET");
        Assert.False(string.IsNullOrWhiteSpace(issuer));
        Assert.False(string.IsNullOrWhiteSpace(signingSecret));

        var options = new JwtBearerOptions();
        SupabaseJwtBearerOptions.Configure(
            options,
            new SupabaseJwtSettings(issuer!, "authenticated", signingSecret!, UseJwks: true));

        var configuration = await options.ConfigurationManager!
            .GetConfigurationAsync(CancellationToken.None);

        Assert.Contains(
            configuration.SigningKeys,
            key => key.KeyId is not null &&
                   (key is JsonWebKey { Alg: SecurityAlgorithms.EcdsaSha256 } ||
                    key is ECDsaSecurityKey));
    }
}
