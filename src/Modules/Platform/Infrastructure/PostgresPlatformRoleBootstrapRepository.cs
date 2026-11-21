using System.Text.Json;
using Elangi.Modules.Audit.Application;
using Elangi.Modules.Platform.Application;
using Npgsql;

namespace Elangi.Modules.Platform.Infrastructure;

public sealed class PostgresPlatformRoleBootstrapRepository(
    NpgsqlDataSource dataSource,
    AuditWriter auditWriter) : IPlatformRoleBootstrapRepository
{
    public async Task<Guid> AssignInitialOwnerAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var roleId = await FindActivePlatformOwnerRoleAsync(
            connection,
            transaction,
            cancellationToken)
            ?? throw new PlatformBootstrapException(
                "The active Platform Owner role is not available.");
        var authUserId = await FindVerifiedAuthUserAsync(
            connection,
            transaction,
            normalizedEmail,
            cancellationToken)
            ?? throw new PlatformBootstrapException(
                "A verified Supabase Auth user with that email was not found.");
        var profileId = await EnsureUserProfileAsync(
            connection,
            transaction,
            authUserId,
            cancellationToken);
        var assignmentId = await InsertPlatformOwnerAssignmentAsync(
            connection,
            transaction,
            profileId,
            roleId,
            cancellationToken);

        if (assignmentId is not null)
        {
            using var summary = JsonDocument.Parse("""{"role":"platform-owner"}""");
            await auditWriter.RecordAsync(
                new AuditEntry(
                    CompanyId: null,
                    ActorUserProfileId: null,
                    Operation: "platform.owner.bootstrapped",
                    TargetType: "UserProfile",
                    TargetId: profileId,
                    NonSecretChangeSummary: summary,
                    OccurredAt: DateTimeOffset.UtcNow),
                connection,
                transaction,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return profileId;
    }

    private static async Task<Guid?> FindActivePlatformOwnerRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM platform.platform_roles
            WHERE code = 'platform-owner'
              AND is_active;
            """,
            connection,
            transaction);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid roleId ? roleId : null;
    }

    private static async Task<Guid?> FindVerifiedAuthUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM auth.users
            WHERE lower(email) = @email
              AND email_confirmed_at IS NOT NULL;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("email", normalizedEmail);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid authUserId ? authUserId : null;
    }

    private static async Task<Guid> EnsureUserProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid authUserId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO security.user_profiles (auth_user_id)
            VALUES (@auth_user_id)
            ON CONFLICT (auth_user_id) DO NOTHING;

            SELECT id
            FROM security.user_profiles
            WHERE auth_user_id = @auth_user_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid profileId
            ? profileId
            : throw new InvalidOperationException("The Auth user profile could not be resolved.");
    }

    private static async Task<Guid?> InsertPlatformOwnerAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid profileId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO platform.user_platform_roles (user_profile_id, role_id)
            VALUES (@profile_id, @role_id)
            ON CONFLICT (user_profile_id, role_id) DO NOTHING
            RETURNING id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("profile_id", profileId);
        command.Parameters.AddWithValue("role_id", roleId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid assignmentId ? assignmentId : null;
    }
}
