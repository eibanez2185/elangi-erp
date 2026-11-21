using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Elangi.Modules.Security.Application;

namespace Elangi.Modules.Platform.Application;

public interface IInvitationEmailSender
{
    Task SendAsync(
        string recipient,
        string companyName,
        Guid companyId,
        Guid invitationId,
        string originalToken,
        CancellationToken cancellationToken);
}

public sealed class InitialAdministratorInvitationService(
    ICurrentActor currentActor,
    PlatformAuthorizationService platformAuthorization,
    IPlatformCompanyRepository repository,
    IInvitationEmailSender emailSender)
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public async Task<InitialAdministratorInvitation> InviteAsync(
        Guid companyId,
        string email,
        CancellationToken cancellationToken)
    {
        var actorProfileId =
            await platformAuthorization.RequirePlatformOwnerAsync(cancellationToken);
        var company = await repository.FindAsync(
            companyId,
            currentActor.UserId,
            cancellationToken);
        if (company is null)
        {
            throw new PlatformResourceNotFoundException("Company was not found.");
        }

        var inviteeEmail = NormalizeEmail(email);
        var token = CreateToken();
        var invitation = await repository.CreateInitialAdministratorInvitationAsync(
            companyId,
            inviteeEmail,
            CreateTokenDigest(token),
            DateTimeOffset.UtcNow.Add(InvitationLifetime),
            currentActor.UserId,
            actorProfileId,
            cancellationToken);

        try
        {
            await emailSender.SendAsync(
                inviteeEmail,
                company.TradeName ?? company.LegalName,
                companyId,
                invitation.Id,
                token,
                cancellationToken);
        }
        catch (Exception deliveryException)
        {
            try
            {
                await repository.RevokeUndeliveredInvitationAsync(
                    companyId,
                    invitation.Id,
                    currentActor.UserId,
                    actorProfileId,
                    CancellationToken.None);
            }
            catch (Exception revocationException)
            {
                throw new AggregateException(
                    "Invitation delivery failed and its pending membership could not be revoked.",
                    deliveryException,
                    revocationException);
            }

            throw;
        }

        return invitation;
    }

    private static string NormalizeEmail(string email)
    {
        try
        {
            return new MailAddress(email.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Invitee email is invalid.", nameof(email), exception);
        }
    }

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string CreateTokenDigest(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
