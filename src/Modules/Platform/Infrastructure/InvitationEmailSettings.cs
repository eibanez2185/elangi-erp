namespace Elangi.Modules.Platform.Infrastructure;

public sealed record InvitationEmailSettings(
    string SmtpHost,
    int SmtpPort,
    string FromAddress,
    string FrontendBaseUrl,
    bool UseSsl,
    string? Username,
    string? Password);
