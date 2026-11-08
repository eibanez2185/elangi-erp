using Elangi.Modules.Security.Application;

namespace Elangi.UnitTests.Security;

public sealed class CompanyAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_AllowsAnActiveMembershipWithTheExactPermission()
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var service = CreateService(
            actorId,
            new CompanyAccessSnapshot(
                actorId,
                companyId,
                CompanyStatus.Active,
                MembershipStatus.Active,
                new HashSet<string>(["company.memberships.read"], StringComparer.Ordinal)));

        await service.AuthorizeAsync(companyId, "company.memberships.read");
    }

    [Theory]
    [InlineData(CompanyStatus.Suspended, MembershipStatus.Active)]
    [InlineData(CompanyStatus.Active, MembershipStatus.Pending)]
    [InlineData(CompanyStatus.Active, MembershipStatus.Suspended)]
    [InlineData(CompanyStatus.Active, MembershipStatus.Revoked)]
    public async Task AuthorizeAsync_DeniesInactiveCompanyOrMembership(
        CompanyStatus companyStatus,
        MembershipStatus membershipStatus)
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var service = CreateService(
            actorId,
            new CompanyAccessSnapshot(
                actorId,
                companyId,
                companyStatus,
                membershipStatus,
                new HashSet<string>(["company.memberships.read"], StringComparer.Ordinal)));

        await Assert.ThrowsAsync<CompanyAccessDeniedException>(
            () => service.AuthorizeAsync(companyId, "company.memberships.read"));
    }

    [Fact]
    public async Task AuthorizeAsync_DoesNotTrustAReaderResultForAnotherActorOrCompany()
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var service = CreateService(
            actorId,
            new CompanyAccessSnapshot(
                Guid.NewGuid(),
                companyId,
                CompanyStatus.Active,
                MembershipStatus.Active,
                new HashSet<string>(["company.memberships.read"], StringComparer.Ordinal)));

        await Assert.ThrowsAsync<CompanyAccessDeniedException>(
            () => service.AuthorizeAsync(companyId, "company.memberships.read"));
    }

    [Fact]
    public async Task AuthorizeAsync_DeniesPermissionsOutsideTheAssignedSet()
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var service = CreateService(
            actorId,
            new CompanyAccessSnapshot(
                actorId,
                companyId,
                CompanyStatus.Active,
                MembershipStatus.Active,
                new HashSet<string>(["company.memberships.read"], StringComparer.Ordinal)));

        await Assert.ThrowsAsync<CompanyAccessDeniedException>(
            () => service.AuthorizeAsync(companyId, "company.memberships.write"));
    }

    private static CompanyAuthorizationService CreateService(
        Guid actorId,
        CompanyAccessSnapshot? access)
    {
        var actor = new TestActor(actorId);
        return new CompanyAuthorizationService(actor, new TestAccessReader(access));
    }

    private sealed class TestActor(Guid userId) : ICurrentActor
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class TestAccessReader(CompanyAccessSnapshot? access) : ICompanyAccessReader
    {
        public Task<CompanyAccessSnapshot?> FindAsync(
            Guid actorUserId,
            Guid companyId,
            CancellationToken cancellationToken) =>
            Task.FromResult(access);
    }
}
