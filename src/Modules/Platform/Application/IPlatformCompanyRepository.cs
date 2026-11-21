using Elangi.Modules.Platform.Domain;

namespace Elangi.Modules.Platform.Application;

public interface IPlatformCompanyRepository
{
    Task<Company> CreateAsync(
        Company company,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Company>> ListAsync(
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<Company?> FindAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<Company?> UpdateAsync(
        Company company,
        IReadOnlySet<string> changedFields,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken);

    Task<Company?> ChangeStatusAsync(
        Guid companyId,
        CompanyStatus status,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken);

    Task<InitialAdministratorInvitation> CreateInitialAdministratorInvitationAsync(
        Guid companyId,
        string inviteeEmail,
        string tokenDigest,
        DateTimeOffset expiresAt,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken);

    Task RevokeUndeliveredInvitationAsync(
        Guid companyId,
        Guid invitationId,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken);
}

public sealed record InitialAdministratorInvitation(
    Guid Id,
    Guid CompanyId,
    string InviteeEmail,
    DateTimeOffset ExpiresAt);

public sealed class PlatformConflictException(string message) : Exception(message)
{
}

public sealed class PlatformResourceNotFoundException(string message) : Exception(message)
{
}

public sealed class InvitationEmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}
