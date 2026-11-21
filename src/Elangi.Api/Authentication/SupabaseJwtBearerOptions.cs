using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Elangi.Api.Authentication;

public static class SupabaseJwtBearerOptions
{
    public static void Configure(JwtBearerOptions options, SupabaseJwtSettings settings)
    {
        options.MapInboundClaims = false;
        var jwksUri = $"{settings.Issuer.TrimEnd('/')}/.well-known/jwks.json";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningSecret)),
            ValidAlgorithms = settings.UseJwks
                ? [SecurityAlgorithms.HmacSha256, SecurityAlgorithms.EcdsaSha256]
                : [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        if (settings.UseJwks)
        {
            var jwksAddress = new Uri(jwksUri);
            options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                jwksUri,
                new SupabaseJwksConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = !jwksAddress.IsLoopback });
            options.RefreshOnIssuerKeyNotFound = true;
        }

        options.Events = ProblemDetailsAuthenticationEvents.Create();
    }
}
