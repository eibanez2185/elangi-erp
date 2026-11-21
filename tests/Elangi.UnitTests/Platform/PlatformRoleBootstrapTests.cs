using Elangi.Modules.Platform.Application;

namespace Elangi.UnitTests.Platform;

public sealed class PlatformRoleBootstrapTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("owner@example.test trailing")]
    public async Task AssignInitialOwnerRejectsInvalidEmail(string email)
    {
        var repository = new RecordingPlatformRoleBootstrapRepository();
        var bootstrap = new PlatformRoleBootstrap(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => bootstrap.AssignInitialOwnerAsync(email));

        Assert.Null(repository.Email);
    }

    [Fact]
    public async Task AssignInitialOwnerNormalizesTheEmailBeforeCallingTheRepository()
    {
        var repository = new RecordingPlatformRoleBootstrapRepository();
        var bootstrap = new PlatformRoleBootstrap(repository);

        var profileId = await bootstrap.AssignInitialOwnerAsync(" Owner@Example.Test ");

        Assert.Equal(repository.ProfileId, profileId);
        Assert.Equal("owner@example.test", repository.Email);
    }

    private sealed class RecordingPlatformRoleBootstrapRepository : IPlatformRoleBootstrapRepository
    {
        public Guid ProfileId { get; } = Guid.NewGuid();
        public string? Email { get; private set; }

        public Task<Guid> AssignInitialOwnerAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            Email = normalizedEmail;
            return Task.FromResult(ProfileId);
        }
    }
}
