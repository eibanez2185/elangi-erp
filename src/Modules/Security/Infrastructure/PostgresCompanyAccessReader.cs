using Elangi.Modules.Security.Application;
using Elangi.Shared.Infrastructure.Postgres;
using Npgsql;

namespace Elangi.Modules.Security.Infrastructure;

public sealed class PostgresCompanyAccessReader(
    TenantTransactionFactory transactionFactory) : ICompanyAccessReader
{
    public async Task<CompanyAccessSnapshot?> FindAsync(
        Guid actorUserId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await transactionFactory.BeginAsync(
            new TenantTransactionContext(actorUserId, companyId),
            cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                profile.auth_user_id,
                company.id,
                company.status,
                membership.status,
                COALESCE(
                    array_agg(DISTINCT permission.code)
                        FILTER (WHERE permission.code IS NOT NULL),
                    ARRAY[]::text[])
            FROM platform.companies AS company
            JOIN security.memberships AS membership
                ON membership.company_id = company.id
            JOIN security.user_profiles AS profile
                ON profile.id = membership.user_profile_id
            LEFT JOIN security.membership_roles AS membership_role
                ON membership_role.company_id = membership.company_id
                AND membership_role.membership_id = membership.id
            LEFT JOIN security.company_roles AS company_role
                ON company_role.company_id = membership_role.company_id
                AND company_role.id = membership_role.role_id
                AND company_role.is_active
            LEFT JOIN security.company_role_permissions AS role_permission
                ON role_permission.company_id = company_role.company_id
                AND role_permission.role_id = company_role.id
            LEFT JOIN platform.permission_catalog AS permission
                ON permission.id = role_permission.permission_id
                AND permission.scope = 'Company'
            WHERE company.id = @company_id
              AND profile.auth_user_id = @actor_user_id
            GROUP BY
                profile.auth_user_id,
                company.id,
                company.status,
                membership.status;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("company_id", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshot = new CompanyAccessSnapshot(
            reader.GetGuid(0),
            reader.GetGuid(1),
            ParseStatus<CompanyStatus>(reader.GetString(2)),
            ParseStatus<MembershipStatus>(reader.GetString(3)),
            reader.GetFieldValue<string[]>(4).ToHashSet(StringComparer.Ordinal));

        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The database returned multiple memberships for one user and company.");
        }

        return snapshot;
    }

    private static TStatus ParseStatus<TStatus>(string value)
        where TStatus : struct, Enum =>
        Enum.TryParse<TStatus>(value, ignoreCase: false, out var status) &&
        Enum.IsDefined(status)
            ? status
            : throw new InvalidOperationException(
                $"The database returned an unsupported {typeof(TStatus).Name} value.");
}
