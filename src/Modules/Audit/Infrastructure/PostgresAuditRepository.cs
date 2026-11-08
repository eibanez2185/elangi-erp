using System.Data;
using System.Data.Common;
using Elangi.Modules.Audit.Application;
using Npgsql;

namespace Elangi.Modules.Audit.Infrastructure;

public sealed class PostgresAuditRepository : IAuditRepository
{
    public async Task AppendAsync(
        AuditEntry entry,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit.audit_records
                (id, company_id, actor_user_profile_id, operation, occurred_at,
                 target_type, target_id, change_summary)
            VALUES
                (@id, @company_id, @actor_user_profile_id, @operation, @occurred_at,
                 @target_type, @target_id, @change_summary::jsonb);
            """;

        AddParameter(command, "id", Guid.NewGuid(), DbType.Guid);
        AddParameter(command, "company_id", entry.CompanyId, DbType.Guid);
        AddParameter(command, "actor_user_profile_id", entry.ActorUserProfileId, DbType.Guid);
        AddParameter(command, "operation", entry.Operation, DbType.String);
        AddParameter(command, "occurred_at", entry.OccurredAt, DbType.DateTimeOffset);
        AddParameter(command, "target_type", entry.TargetType, DbType.String);
        AddParameter(command, "target_id", entry.TargetId, DbType.Guid);
        AddParameter(
            command,
            "change_summary",
            entry.NonSecretChangeSummary?.RootElement.GetRawText(),
            DbType.String);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value,
        DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
