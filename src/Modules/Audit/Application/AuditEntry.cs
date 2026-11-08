using System.Text.Json;

namespace Elangi.Modules.Audit.Application;

public sealed record AuditEntry(
    Guid? CompanyId,
    Guid? ActorUserProfileId,
    string Operation,
    string TargetType,
    Guid? TargetId,
    JsonDocument? NonSecretChangeSummary,
    DateTimeOffset OccurredAt);
