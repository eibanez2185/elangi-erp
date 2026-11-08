namespace Elangi.Modules.Security.Application;

public sealed class CompanyAuthorizationService(
    ICurrentActor currentActor,
    ICompanyAccessReader accessReader)
{
    public async Task AuthorizeAsync(
        Guid companyId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || string.IsNullOrWhiteSpace(permissionCode))
        {
            throw new ArgumentException("A company and permission are required.");
        }

        var access = await accessReader.FindAsync(
            currentActor.UserId,
            companyId,
            cancellationToken);

        if (access is null ||
            access.CompanyId != companyId ||
            access.ActorUserId != currentActor.UserId ||
            access.CompanyStatus != CompanyStatus.Active ||
            access.MembershipStatus != MembershipStatus.Active ||
            !access.PermissionCodes.Contains(permissionCode, StringComparer.Ordinal))
        {
            throw new CompanyAccessDeniedException();
        }
    }
}

public interface ICompanyAccessReader
{
    Task<CompanyAccessSnapshot?> FindAsync(
        Guid actorUserId,
        Guid companyId,
        CancellationToken cancellationToken);
}

public sealed record CompanyAccessSnapshot(
    Guid ActorUserId,
    Guid CompanyId,
    CompanyStatus CompanyStatus,
    MembershipStatus MembershipStatus,
    IReadOnlySet<string> PermissionCodes);

public enum CompanyStatus
{
    Active,
    Suspended
}

public enum MembershipStatus
{
    Pending,
    Active,
    Suspended,
    Revoked
}

public sealed class CompanyAccessDeniedException : Exception
{
}
