namespace Elangi.Shared.Infrastructure.Postgres;

public sealed record TenantTransactionContext(
    Guid ActorUserId,
    Guid? CompanyId,
    bool IsPlatformOwner = false);
