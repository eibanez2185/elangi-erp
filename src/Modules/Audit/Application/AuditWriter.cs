using System.Data.Common;

namespace Elangi.Modules.Audit.Application;

public sealed class AuditWriter(IAuditRepository repository)
{
    public Task RecordAsync(
        AuditEntry entry,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (string.IsNullOrWhiteSpace(entry.Operation) ||
            string.IsNullOrWhiteSpace(entry.TargetType))
        {
            throw new ArgumentException("Audit operation and target type are required.", nameof(entry));
        }

        return repository.AppendAsync(entry, connection, transaction, cancellationToken);
    }
}
