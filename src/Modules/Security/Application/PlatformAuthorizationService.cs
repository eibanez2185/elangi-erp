namespace Elangi.Modules.Security.Application;

public sealed class PlatformAuthorizationService(
    ICurrentActor currentActor,
    IPlatformAccessReader accessReader)
{
    public async Task<Guid> RequirePlatformOwnerAsync(
        CancellationToken cancellationToken = default)
    {
        var profileId = await accessReader.FindPlatformOwnerProfileIdAsync(
            currentActor.UserId,
            cancellationToken);

        return profileId is { } id && id != Guid.Empty
            ? id
            : throw new PlatformAccessDeniedException();
    }
}

public interface IPlatformAccessReader
{
    Task<Guid?> FindPlatformOwnerProfileIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken);
}

public sealed class PlatformAccessDeniedException : Exception
{
}
