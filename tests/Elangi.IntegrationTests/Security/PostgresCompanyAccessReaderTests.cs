using System.Net;
using Elangi.Modules.Security.Application;
using Elangi.Modules.Security.Infrastructure;
using Elangi.Shared.Infrastructure.Postgres;
using Npgsql;

namespace Elangi.IntegrationTests.Security;

[Collection("Local Supabase")]
public sealed class PostgresCompanyAccessReaderTests
{
    private const string GrantedPermission = "company.memberships.read";

    [Fact]
    public async Task AuthorizationMigrationKeepsRuntimeRoleRestrictedAndForcesRls()
    {
        await using var dataSource = CreateDataSource(GetDatabaseConnectionString());
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT role.rolsuper,
                   role.rolbypassrls,
                   role.rolcanlogin,
                   company.relrowsecurity,
                   company.relforcerowsecurity,
                   has_table_privilege('elangi_runtime', company.oid, 'SELECT'),
                   has_table_privilege('elangi_runtime', company.oid, 'INSERT'),
                   has_table_privilege('elangi_runtime', company.oid, 'UPDATE'),
                   has_table_privilege('elangi_runtime', company.oid, 'DELETE')
            FROM pg_roles AS role
            JOIN pg_class AS company ON company.oid = 'platform.companies'::regclass
            WHERE role.rolname = 'elangi_runtime';
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
        Assert.True(reader.GetBoolean(5));
        Assert.True(reader.GetBoolean(6));
        Assert.True(reader.GetBoolean(7));
        Assert.False(reader.GetBoolean(8));
    }

    [Fact]
    public async Task AuthorizationUsesThePersistedIdentityMembershipAndRolePermission()
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        await using var dataSource = CreateDataSource(GetDatabaseConnectionString());
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);

        try
        {
            await SeedAuthorizedMembershipAsync(
                dataSource,
                actorId,
                companyId,
                profileId,
                membershipId,
                roleId,
                permissionId);

            var service = CreateAuthorizationService(dataSource, actorId);
            await service.AuthorizeAsync(companyId, GrantedPermission);
            await Assert.ThrowsAsync<CompanyAccessDeniedException>(
                () => service.AuthorizeAsync(companyId, "company.memberships.write"));

            var snapshot = await new PostgresCompanyAccessReader(
                    new TenantTransactionFactory(dataSource))
                .FindAsync(actorId, companyId, CancellationToken.None);
            Assert.NotNull(snapshot);
            Assert.Equal(actorId, snapshot.ActorUserId);
            Assert.Equal(companyId, snapshot.CompanyId);
            Assert.Equal(CompanyStatus.Active, snapshot.CompanyStatus);
            Assert.Equal(MembershipStatus.Active, snapshot.MembershipStatus);
            Assert.Contains(GrantedPermission, snapshot.PermissionCodes);
        }
        finally
        {
            await DeleteIdentityAndCompanyAsync(dataSource, actorId, companyId);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    [Fact]
    public async Task AuthorizationAndRlsDenyACompanyWithoutTheActorsMembership()
    {
        var actorId = Guid.NewGuid();
        var actorCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        await using var dataSource = CreateDataSource(GetDatabaseConnectionString());
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);

        try
        {
            await SeedActorAndCompaniesAsync(
                dataSource,
                actorId,
                actorCompanyId,
                otherCompanyId,
                profileId,
                membershipId);

            var service = CreateAuthorizationService(dataSource, actorId);
            await Assert.ThrowsAsync<CompanyAccessDeniedException>(
                () => service.AuthorizeAsync(otherCompanyId, GrantedPermission));

            await using var transaction = await TenantTransaction.BeginAsync(
                dataSource,
                new TenantTransactionContext(actorId, otherCompanyId));
            await using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction.Transaction;
            command.CommandText = "SELECT count(*) FROM platform.companies WHERE id = @company_id;";
            command.Parameters.AddWithValue("company_id", otherCompanyId);

            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await DeleteIdentityAndCompanyAsync(dataSource, actorId, actorCompanyId, otherCompanyId);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    [Fact]
    public async Task PlatformOwnerRoleWithoutCompanyMembershipDoesNotAuthorizeCompanyAccess()
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        await using var dataSource = CreateDataSource(GetDatabaseConnectionString());
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);

        try
        {
            await SeedPlatformOwnerWithoutMembershipAsync(
                dataSource,
                actorId,
                companyId,
                profileId);

            var service = CreateAuthorizationService(dataSource, actorId);
            await Assert.ThrowsAsync<CompanyAccessDeniedException>(
                () => service.AuthorizeAsync(companyId, GrantedPermission));
        }
        finally
        {
            await DeleteIdentityAndCompanyAsync(dataSource, actorId, companyId);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    [Theory]
    [InlineData("Suspended", "Active")]
    [InlineData("Active", "Suspended")]
    [InlineData("Active", "Revoked")]
    [InlineData("Active", "Pending")]
    public async Task AuthorizationDeniesPersistedInactiveCompanyOrMembership(
        string companyStatus,
        string membershipStatus)
    {
        var actorId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        await using var dataSource = CreateDataSource(GetDatabaseConnectionString());
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);

        try
        {
            await SeedAuthorizedMembershipAsync(
                dataSource,
                actorId,
                companyId,
                profileId,
                membershipId,
                roleId,
                permissionId,
                companyStatus,
                membershipStatus);

            var service = CreateAuthorizationService(dataSource, actorId);
            await Assert.ThrowsAsync<CompanyAccessDeniedException>(
                () => service.AuthorizeAsync(companyId, GrantedPermission));
        }
        finally
        {
            await DeleteIdentityAndCompanyAsync(dataSource, actorId, companyId);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    private static CompanyAuthorizationService CreateAuthorizationService(
        NpgsqlDataSource dataSource,
        Guid actorId) =>
        new(
            new TestActor(actorId),
            new PostgresCompanyAccessReader(new TenantTransactionFactory(dataSource)));

    private static async Task SeedAuthorizedMembershipAsync(
        NpgsqlDataSource dataSource,
        Guid actorId,
        Guid companyId,
        Guid profileId,
        Guid membershipId,
        Guid roleId,
        Guid permissionId,
        string companyStatus = "Active",
        string membershipStatus = "Active")
    {
        await SeedActorAndCompaniesAsync(
            dataSource,
            actorId,
            companyId,
            null,
            profileId,
            membershipId,
            companyStatus,
            membershipStatus);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO platform.permission_catalog (id, code, description, scope)
            VALUES (@permission_id, @permission_code, 'Integration test permission', 'Company');

            INSERT INTO security.company_roles (id, company_id, name)
            VALUES (@role_id, @company_id, 'Integration test role');

            INSERT INTO security.membership_roles (company_id, membership_id, role_id)
            VALUES (@company_id, @membership_id, @role_id);

            INSERT INTO security.company_role_permissions
                (company_id, role_id, permission_id)
            VALUES (@company_id, @role_id, @permission_id);
            """,
            connection);
        command.Parameters.AddWithValue("permission_id", permissionId);
        command.Parameters.AddWithValue("permission_code", GrantedPermission);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("membership_id", membershipId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedActorAndCompaniesAsync(
        NpgsqlDataSource dataSource,
        Guid actorId,
        Guid actorCompanyId,
        Guid? otherCompanyId,
        Guid profileId,
        Guid? membershipId,
        string companyStatus = "Active",
        string membershipStatus = "Active")
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO auth.users (id, email, created_at, updated_at)
            VALUES (@actor_id, @email, now(), now());

            INSERT INTO security.user_profiles (id, auth_user_id, display_name)
            VALUES (@profile_id, @actor_id, 'Integration test actor');

            INSERT INTO platform.companies (id, status, legal_name, country_code)
            VALUES (@actor_company_id, @company_status, 'Integration Test Company', 'US');
            """,
            connection);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("email", $"{actorId:N}@integration.invalid");
        command.Parameters.AddWithValue("profile_id", profileId);
        command.Parameters.AddWithValue("actor_company_id", actorCompanyId);
        command.Parameters.AddWithValue("company_status", companyStatus);
        if (membershipId is not null)
        {
            command.CommandText +=
                """

                INSERT INTO security.memberships (id, company_id, user_profile_id, status)
                VALUES (@membership_id, @actor_company_id, @profile_id, @membership_status);
                """;
            command.Parameters.AddWithValue("membership_id", membershipId.Value);
            command.Parameters.AddWithValue("membership_status", membershipStatus);
        }

        if (otherCompanyId is not null)
        {
            command.CommandText +=
                """

                INSERT INTO platform.companies (id, status, legal_name, country_code)
                VALUES (@other_company_id, 'Active', 'Other Integration Company', 'US');
                """;
            command.Parameters.AddWithValue("other_company_id", otherCompanyId.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedPlatformOwnerWithoutMembershipAsync(
        NpgsqlDataSource dataSource,
        Guid actorId,
        Guid companyId,
        Guid profileId)
    {
        await SeedActorAndCompaniesAsync(
            dataSource,
            actorId,
            companyId,
            null,
            profileId,
            null);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO platform.user_platform_roles (user_profile_id, role_id)
            SELECT @profile_id, id
            FROM platform.platform_roles
            WHERE code = 'platform-owner' AND is_active;
            """,
            connection);
        command.Parameters.AddWithValue("profile_id", profileId);
        if (await command.ExecuteNonQueryAsync() != 1)
        {
            throw new InvalidOperationException("The Platform Owner bootstrap role is not seeded.");
        }
    }

    private static async Task DeleteIdentityAndCompanyAsync(
        NpgsqlDataSource dataSource,
        Guid actorId,
        params Guid[] companyIds)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM platform.user_platform_roles
            WHERE user_profile_id IN (
                SELECT id FROM security.user_profiles WHERE auth_user_id = @actor_id
            );

            DELETE FROM security.company_role_permissions
            WHERE company_id = ANY (@company_ids);

            DELETE FROM security.membership_roles
            WHERE company_id = ANY (@company_ids);

            DELETE FROM security.memberships
            WHERE company_id = ANY (@company_ids);

            DELETE FROM security.company_roles
            WHERE company_id = ANY (@company_ids);

            DELETE FROM platform.permission_catalog
            WHERE code = @permission_code;

            DELETE FROM auth.users WHERE id = @actor_id;
            DELETE FROM platform.companies WHERE id = ANY (@company_ids);
            """,
            connection);
        command.Parameters.AddWithValue("permission_code", GrantedPermission);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("company_ids", companyIds);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task GrantRuntimeRoleToCurrentUserAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("GRANT elangi_runtime TO CURRENT_USER;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RevokeRuntimeRoleFromCurrentUserAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("REVOKE elangi_runtime FROM CURRENT_USER;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string GetDatabaseConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE by dot-sourcing supabase/scripts/load-local-environment.ps1 after starting local Supabase.");
        }

        return connectionString;
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var databaseUri) ||
            databaseUri.Scheme is not ("postgres" or "postgresql"))
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                throw new InvalidOperationException(
                    "ELANGI_TEST_DATABASE must include a local database host.");
            }

            EnsureLocalDatabase(builder.Host, builder.Port);
            return NpgsqlDataSource.Create(builder.ConnectionString);
        }

        EnsureLocalDatabase(databaseUri.Host, databaseUri.IsDefaultPort ? 5432 : databaseUri.Port);
        var credentials = databaseUri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("ELANGI_TEST_DATABASE must include local database credentials.");
        }

        return NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
            Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        }.ConnectionString);
    }

    private static void EnsureLocalDatabase(string host, int port)
    {
        var isLoopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
        if (!isLoopback || port != 54322)
        {
            throw new InvalidOperationException(
                "Integration tests are restricted to the local Supabase database at port 54322.");
        }
    }

    private sealed class TestActor(Guid userId) : ICurrentActor
    {
        public Guid UserId { get; } = userId;
    }
}
