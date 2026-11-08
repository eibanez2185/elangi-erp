using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Elangi.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
}
