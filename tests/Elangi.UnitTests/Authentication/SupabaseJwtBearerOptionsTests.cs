using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elangi.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Elangi.UnitTests.Authentication;

public sealed class SupabaseJwtBearerOptionsTests
{
    private const string SigningSecret = "unit-test-signing-secret-that-is-never-a-real-key";
    private const string Issuer = "http://localhost:54321/auth/v1";

    [Fact]
    public void Configure_ValidatesIssuerAudienceSignatureAndSubject()
    {
        var settings = new SupabaseJwtSettings(Issuer, "authenticated", SigningSecret);
        var options = new JwtBearerOptions();
        SupabaseJwtBearerOptions.Configure(options, settings);
        var userId = Guid.NewGuid();
        var token = CreateToken(SigningSecret, Issuer, "authenticated", userId);

        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(
            token,
            options.TokenValidationParameters,
            out _);

        Assert.Equal(userId.ToString(), principal.FindFirst("sub")?.Value);
    }

    [Fact]
    public void Configure_RejectsATokenSignedWithAnotherKey()
    {
        var settings = new SupabaseJwtSettings(Issuer, "authenticated", SigningSecret);
        var options = new JwtBearerOptions();
        SupabaseJwtBearerOptions.Configure(options, settings);
        var token = CreateToken(
            "a-different-unit-test-signing-secret",
            Issuer,
            "authenticated",
            Guid.NewGuid());

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(
            () => new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(
                token,
                options.TokenValidationParameters,
                out _));
    }

    [Fact]
    public void Configure_EnablesSupabaseJwksAndBothSupportedAlgorithms()
    {
        var settings = new SupabaseJwtSettings(Issuer, "authenticated", SigningSecret, UseJwks: true);
        var options = new JwtBearerOptions();

        SupabaseJwtBearerOptions.Configure(options, settings);

        Assert.NotNull(options.ConfigurationManager);
        Assert.True(options.RefreshOnIssuerKeyNotFound);
        Assert.Contains(
            SecurityAlgorithms.EcdsaSha256,
            options.TokenValidationParameters.ValidAlgorithms!);
        Assert.Contains(
            SecurityAlgorithms.HmacSha256,
            options.TokenValidationParameters.ValidAlgorithms!);
    }

    [Fact]
    public async Task Configure_ValidatesAnEs256TokenUsingTheRetrievedJwksKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicParameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var keyId = "unit-test-es256-key";
        var jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "EC",
                    crv = "P-256",
                    x = Base64UrlEncoder.Encode(publicParameters.Q.X!),
                    y = Base64UrlEncoder.Encode(publicParameters.Q.Y!),
                    kid = keyId,
                    alg = SecurityAlgorithms.EcdsaSha256,
                    use = "sig"
                }
            }
        });
        var configuration = await new SupabaseJwksConfigurationRetriever()
            .GetConfigurationAsync(
                "https://issuer.example.test/auth/v1/.well-known/jwks.json",
                new StaticDocumentRetriever(jwks),
                CancellationToken.None);
        var settings = new SupabaseJwtSettings(Issuer, "authenticated", SigningSecret, UseJwks: true);
        var options = new JwtBearerOptions();
        SupabaseJwtBearerOptions.Configure(options, settings);
        var userId = Guid.NewGuid();
        var signingKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
        var token = new JwtSecurityToken(
            Issuer,
            "authenticated",
            [new Claim("sub", userId.ToString())],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(signingKey, SecurityAlgorithms.EcdsaSha256));
        var validationParameters = options.TokenValidationParameters.Clone();
        validationParameters.IssuerSigningKeys = configuration.SigningKeys;

        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            validationParameters,
            out _);

        Assert.Equal(userId.ToString(), principal.FindFirst("sub")?.Value);
    }

    private static string CreateToken(
        string secret,
        string issuer,
        string audience,
        Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim("sub", userId.ToString())],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StaticDocumentRetriever(string document) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel) =>
            Task.FromResult(document);
    }
}
