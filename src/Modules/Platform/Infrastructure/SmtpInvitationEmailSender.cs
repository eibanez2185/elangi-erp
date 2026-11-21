using System.Net;
using System.Net.Mail;
using Elangi.Modules.Platform.Application;

namespace Elangi.Modules.Platform.Infrastructure;

public sealed class SmtpInvitationEmailSender(InvitationEmailSettings settings)
    : IInvitationEmailSender
{
    public async Task SendAsync(
        string recipient,
        string companyName,
        Guid companyId,
        Guid invitationId,
        string originalToken,
        CancellationToken cancellationToken)
    {
        var invitationUrl = BuildInvitationUrl(companyId, invitationId, originalToken);
        using var message = new MailMessage(
            new MailAddress(settings.FromAddress),
            new MailAddress(recipient))
        {
            Subject = $"Invitation to administer {companyName}",
            Body = $"You have been invited to administer {companyName}.\r\n\r\n" +
                   $"Accept the invitation by visiting:\r\n{invitationUrl}\r\n\r\n" +
                   "This link expires in seven days.",
            IsBodyHtml = false
        };

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException exception)
        {
            throw new InvitationEmailDeliveryException(
                "The invitation email could not be delivered.",
                exception);
        }
    }

    private string BuildInvitationUrl(Guid companyId, Guid invitationId, string originalToken)
    {
        var url = new UriBuilder(settings.FrontendBaseUrl)
        {
            Fragment =
                $"accept-invitation?companyId={Uri.EscapeDataString(companyId.ToString())}" +
                $"&invitationId={Uri.EscapeDataString(invitationId.ToString())}" +
                $"&token={Uri.EscapeDataString(originalToken)}"
        };

        return url.Uri.ToString();
    }
}
