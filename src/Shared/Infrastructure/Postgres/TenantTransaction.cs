using System.Data;
using Npgsql;

namespace Elangi.Shared.Infrastructure.Postgres;

public sealed class TenantTransaction : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;

    private TenantTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    public NpgsqlConnection Connection => connection;
    public NpgsqlTransaction Transaction => transaction;

    public static async ValueTask<TenantTransaction> BeginAsync(
        NpgsqlDataSource dataSource,
        TenantTransactionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ActorUserId == Guid.Empty)
        {
            throw new ArgumentException("A verified actor ID is required.", nameof(context));
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            await using (var roleCommand = new NpgsqlCommand(
                "SET LOCAL ROLE elangi_runtime;",
                connection,
                transaction))
            {
                await roleCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var contextCommand = new NpgsqlCommand(
                """
                SELECT
                    set_config('app.current_actor_id', @actor_id, true),
                    set_config('app.current_company_id', @company_id, true),
                    set_config('app.is_platform_owner', @is_platform_owner, true);
                """,
                connection,
                transaction))
            {
                contextCommand.Parameters.AddWithValue("actor_id", context.ActorUserId.ToString());
                contextCommand.Parameters.AddWithValue(
                    "company_id",
                    context.CompanyId?.ToString() ?? string.Empty);
                contextCommand.Parameters.AddWithValue(
                    "is_platform_owner",
                    context.IsPlatformOwner ? "true" : "false");
                await contextCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return new TenantTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await connection.DisposeAsync();
    }
}
