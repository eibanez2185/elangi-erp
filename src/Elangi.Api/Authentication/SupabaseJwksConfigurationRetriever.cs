using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Elangi.Api.Authentication;

public sealed class SupabaseJwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancellationToken)
    {
        var document = await retriever.GetDocumentAsync(address, cancellationToken);
        var signingKeys = new JsonWebKeySet(document).GetSigningKeys();
        if (signingKeys.Count == 0)
        {
            throw new InvalidOperationException("Supabase JWKS did not contain signing keys.");
        }

        var configuration = new OpenIdConnectConfiguration();
        foreach (var signingKey in signingKeys)
        {
            configuration.SigningKeys.Add(signingKey);
        }

        return configuration;
    }
}
