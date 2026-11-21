using Elangi.Modules.Audit.Application;
using Elangi.Modules.Audit.Infrastructure;
using Elangi.Modules.Platform.Application;
using Elangi.Modules.Platform.Infrastructure;
using Npgsql;

namespace Elangi.IntegrationTests.Security;

[Collection("Local Supabase")]
public sealed class PlatformRoleBootstrapTests
{
    [Fact]
    public async Task AssignsOnlyTheVerifiedAuthUserAndRecordsBootstrapAudit()
    {
        var authUserId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@bootstrap.invalid";
        await using var dataSource = CreateLocalDataSource();
        await InsertAuthUserAsync(dataSource, authUserId, email, emailConfirmedAt: DateTimeOffset.UtcNow);

        try
        {
            var bootstrap = CreateBootstrap(dataSource);
            var profileId = await bootstrap.AssignInitialOwnerAsync(email.ToUpperInvariant());

            Assert.NotEqual(Guid.Empty, profileId);
            Assert.Equal(
                profileId,
                await FindProfileIdAsync(dataSource, authUserId));
            Assert.Equal(
                1L,
                await CountOwnerAssignmentsAsync(dataSource, profileId));
            Assert.Equal(
                0L,
                await CountMembershipsAsync(dataSource, profileId));
            Assert.Equal(
                1L,
                await CountBootstrapAuditRecordsAsync(dataSource, profileId));
            Assert.True(await HasNonSecretBootstrapAuditAsync(dataSource, profileId));

            Assert.Equal(
                profileId,
                await bootstrap.AssignInitialOwnerAsync(email));
            Assert.Equal(
                1L,
                await CountBootstrapAuditRecordsAsync(dataSource, profileId));

            await using var privilegeConnection = await dataSource.OpenConnectionAsync();
            await using var privilegeCommand = new NpgsqlCommand(
                """
                SELECT has_table_privilege(
                           'elangi_runtime',
                           'platform.user_platform_roles',
                           'INSERT'
                       )
                    OR has_table_privilege(
                           'elangi_runtime',
                           'platform.user_platform_roles',
                           'UPDATE'
                       )
                    OR has_table_privilege(
                           'elangi_runtime',
                           'platform.user_platform_roles',
                           'DELETE'
                       );
                """,
                privilegeConnection);
            Assert.False((bool)(await privilegeCommand.ExecuteScalarAsync())!);
        }
        finally
        {
            await DeleteAuthUserAsync(dataSource, authUserId);
        }
    }

    [Fact]
    public async Task DoesNotBootstrapAnUnverifiedAuthIdentity()
    {
        var authUserId = Guid.NewGuid();
        var email = $"{Guid.NewGuid():N}@bootstrap.invalid";
        await using var dataSource = CreateLocalDataSource();
        await InsertAuthUserAsync(dataSource, authUserId, email, emailConfirmedAt: null);

        try
        {
            var bootstrap = CreateBootstrap(dataSource);

            await Assert.ThrowsAsync<PlatformBootstrapException>(
                () => bootstrap.AssignInitialOwnerAsync(email));

            Assert.Null(await FindProfileIdOrNullAsync(dataSource, authUserId));
        }
        finally
        {
            await DeleteAuthUserAsync(dataSource, authUserId);
        }
    }

    private static PlatformRoleBootstrap CreateBootstrap(NpgsqlDataSource dataSource) =>
        new(new PostgresPlatformRoleBootstrapRepository(
            dataSource,
            new AuditWriter(new PostgresAuditRepository())));

    private static async Task InsertAuthUserAsync(
        NpgsqlDataSource dataSource,
        Guid authUserId,
        string email,
        DateTimeOffset? emailConfirmedAt)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO auth.users (id, email, email_confirmed_at, created_at, updated_at)
            VALUES (@id, @email, @email_confirmed_at, now(), now());
            """,
            connection);
        command.Parameters.AddWithValue("id", authUserId);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue(
            "email_confirmed_at",
            emailConfirmedAt is null ? DBNull.Value : emailConfirmedAt.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> FindProfileIdAsync(
        NpgsqlDataSource dataSource,
        Guid authUserId) =>
        await FindProfileIdOrNullAsync(dataSource, authUserId)
            ?? throw new InvalidOperationException("The bootstrap profile was not persisted.");

    private static async Task<Guid?> FindProfileIdOrNullAsync(
        NpgsqlDataSource dataSource,
        Guid authUserId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM security.user_profiles WHERE auth_user_id = @auth_user_id;",
            connection);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        var result = await command.ExecuteScalarAsync();
        return result is Guid profileId ? profileId : null;
    }

    private static async Task<long> CountOwnerAssignmentsAsync(
        NpgsqlDataSource dataSource,
        Guid profileId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM platform.user_platform_roles AS assignment
            JOIN platform.platform_roles AS role ON role.id = assignment.role_id
            WHERE assignment.user_profile_id = @profile_id
              AND role.code = 'platform-owner'
              AND role.is_active;
            """,
            connection);
        command.Parameters.AddWithValue("profile_id", profileId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountBootstrapAuditRecordsAsync(
        NpgsqlDataSource dataSource,
        Guid profileId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM audit.audit_records
            WHERE operation = 'platform.owner.bootstrapped'
              AND target_type = 'UserProfile'
              AND target_id = @profile_id;
            """,
            connection);
        command.Parameters.AddWithValue("profile_id", profileId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountMembershipsAsync(
        NpgsqlDataSource dataSource,
        Guid profileId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM security.memberships WHERE user_profile_id = @profile_id;",
            connection);
        command.Parameters.AddWithValue("profile_id", profileId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> HasNonSecretBootstrapAuditAsync(
        NpgsqlDataSource dataSource,
        Guid profileId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT actor_user_profile_id IS NULL
               AND change_summary = '{"role":"platform-owner"}'::jsonb
            FROM audit.audit_records
            WHERE operation = 'platform.owner.bootstrapped'
              AND target_type = 'UserProfile'
              AND target_id = @profile_id;
            """,
            connection);
        command.Parameters.AddWithValue("profile_id", profileId);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteAuthUserAsync(
        NpgsqlDataSource dataSource,
        Guid authUserId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM platform.user_platform_roles
            WHERE user_profile_id IN (
                SELECT id
                FROM security.user_profiles
                WHERE auth_user_id = @auth_user_id
            );
            DELETE FROM audit.audit_records
            WHERE operation = 'platform.owner.bootstrapped'
              AND target_id IN (
                SELECT id
                FROM security.user_profiles
                WHERE auth_user_id = @auth_user_id
            );
            DELETE FROM auth.users WHERE id = @auth_user_id;
            """,
            connection);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlDataSource CreateLocalDataSource()
    {
        var value = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var databaseUri) ||
            databaseUri.Scheme is not ("postgres" or "postgresql") ||
            databaseUri.Host is not ("127.0.0.1" or "localhost" or "::1") ||
            databaseUri.Port != 54322)
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE to the local Supabase database at 127.0.0.1:54322.");
        }

        var credentials = databaseUri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException(
                "ELANGI_TEST_DATABASE must include local database credentials.");
        }

        return NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.Port,
            Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        }.ConnectionString);
    }
}
