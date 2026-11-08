using System.Data.Common;

namespace Elangi.Modules.Audit.Application;

public interface IAuditRepository
{
    Task AppendAsync(
        AuditEntry entry,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken);
}
