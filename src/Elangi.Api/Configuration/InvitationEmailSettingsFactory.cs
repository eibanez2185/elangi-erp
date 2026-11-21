using System.Net.Mail;
using Elangi.Modules.Platform.Infrastructure;

namespace Elangi.Api.Configuration;

public static class InvitationEmailSettingsFactory
{
    public static InvitationEmailSettings Create(IConfiguration configuration)
    {
        var section = configuration.GetSection("InvitationEmail");
        var host = Required(section["SmtpHost"], "InvitationEmail:SmtpHost");
        var from = Required(section["FromAddress"], "InvitationEmail:FromAddress");
        var frontendUrl = Required(section["FrontendBaseUrl"], "InvitationEmail:FrontendBaseUrl");
        if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var parsedFrontendUrl) ||
            parsedFrontendUrl.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "InvitationEmail:FrontendBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!int.TryParse(section["SmtpPort"], out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("InvitationEmail:SmtpPort must be a valid TCP port.");
        }

        if (!MailAddress.TryCreate(from, out _))
        {
            throw new InvalidOperationException("InvitationEmail:FromAddress must be a valid email address.");
        }

        if (!bool.TryParse(section["UseSsl"], out var useSsl))
        {
            throw new InvalidOperationException("InvitationEmail:UseSsl must be explicitly configured.");
        }

        var username = section["Username"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password) ||
            (!string.IsNullOrWhiteSpace(username) && !useSsl))
        {
            throw new InvalidOperationException(
                "SMTP credentials must be configured together and used with TLS.");
        }

        return new InvitationEmailSettings(
            host,
            port,
            from,
            parsedFrontendUrl.ToString(),
            useSsl,
            username,
            password);
    }

    private static string Required(string? value, string key) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} must be configured.")
            : value.Trim();
}
