using Elangi.Modules.Security.Application;
using Elangi.Shared.Infrastructure.Postgres;
using Npgsql;

namespace Elangi.Modules.Security.Infrastructure;

public sealed class PostgresPlatformAccessReader(
    TenantTransactionFactory transactionFactory) : IPlatformAccessReader
{
    public async Task<Guid?> FindPlatformOwnerProfileIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await transactionFactory.BeginAsync(
            new TenantTransactionContext(actorUserId, null),
            cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT profile.id
            FROM security.user_profiles AS profile
            JOIN platform.user_platform_roles AS assignment
                ON assignment.user_profile_id = profile.id
            JOIN platform.platform_roles AS platform_role
                ON platform_role.id = assignment.role_id
            WHERE profile.auth_user_id = @actor_user_id
              AND platform_role.code = 'platform-owner'
              AND platform_role.is_active;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid profileId ? profileId : null;
    }
}
