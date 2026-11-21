using Elangi.Api.Authentication;
using Microsoft.Extensions.Configuration;

namespace Elangi.UnitTests.Authentication;

public sealed class SupabaseJwtSettingsTests
{
    [Fact]
    public void FromConfiguration_RequiresIssuerAndSecret()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPABASE_JWT_ISSUER"] = "http://localhost:54321/auth/v1",
                ["SUPABASE_JWT_SECRET"] = "a-local-test-signing-secret-that-is-never-committed"
            })
            .Build();

        var settings = SupabaseJwtSettings.FromConfiguration(configuration);

        Assert.Equal("http://localhost:54321/auth/v1", settings.Issuer);
        Assert.Equal("authenticated", settings.Audience);
    }

    [Fact]
    public void FromConfiguration_FailsWhenSigningSecretIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPABASE_JWT_ISSUER"] = "http://localhost:54321/auth/v1"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => SupabaseJwtSettings.FromConfiguration(configuration));
    }

    [Fact]
    public void FromConfiguration_EnablesJwksForSupabaseAsymmetricSigning()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPABASE_JWT_ISSUER"] = "http://localhost:54321/auth/v1",
                ["SUPABASE_JWT_SECRET"] = "a-local-test-signing-secret-that-is-never-committed",
                ["SUPABASE_JWT_USE_JWKS"] = "true"
            })
            .Build();

        var settings = SupabaseJwtSettings.FromConfiguration(configuration);

        Assert.True(settings.UseJwks);
    }

    [Fact]
    public void FromConfiguration_RejectsInvalidJwksSetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPABASE_JWT_ISSUER"] = "http://localhost:54321/auth/v1",
                ["SUPABASE_JWT_SECRET"] = "a-local-test-signing-secret-that-is-never-committed",
                ["SUPABASE_JWT_USE_JWKS"] = "enabled"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => SupabaseJwtSettings.FromConfiguration(configuration));
    }

}
