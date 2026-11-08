using Npgsql;

namespace Elangi.Shared.Infrastructure.Postgres;

public sealed class TenantTransactionFactory(NpgsqlDataSource dataSource)
{
    public ValueTask<TenantTransaction> BeginAsync(
        TenantTransactionContext context,
        CancellationToken cancellationToken = default) =>
        TenantTransaction.BeginAsync(dataSource, context, cancellationToken);
}
