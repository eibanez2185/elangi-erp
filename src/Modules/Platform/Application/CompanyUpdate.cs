using Elangi.Modules.Platform.Domain;

namespace Elangi.Modules.Platform.Application;

public readonly record struct OptionalField<T>(bool IsSpecified, T Value);

public sealed record CompanyUpdate(
    OptionalField<string?> LegalName,
    OptionalField<string?> TradeName,
    OptionalField<string?> CountryCode,
    OptionalField<string?> LegalIdentifier,
    OptionalField<string?> Address,
    OptionalField<CompanyContactDetails?> ContactDetails,
    OptionalField<string?> SubscriptionPlanReference,
    OptionalField<string?> SubscriptionStatus,
    OptionalField<DateTimeOffset?> SubscriptionStartedAt,
    OptionalField<DateTimeOffset?> SubscriptionEndsAt)
{
    public bool HasIdentityChanges =>
        LegalName.IsSpecified ||
        TradeName.IsSpecified ||
        CountryCode.IsSpecified ||
        LegalIdentifier.IsSpecified ||
        Address.IsSpecified ||
        ContactDetails.IsSpecified;

    public bool HasSubscriptionChanges =>
        SubscriptionPlanReference.IsSpecified ||
        SubscriptionStatus.IsSpecified ||
        SubscriptionStartedAt.IsSpecified ||
        SubscriptionEndsAt.IsSpecified;

    public IReadOnlySet<string> ChangedFields()
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        AddIfSpecified(fields, nameof(LegalName), LegalName.IsSpecified);
        AddIfSpecified(fields, nameof(TradeName), TradeName.IsSpecified);
        AddIfSpecified(fields, nameof(CountryCode), CountryCode.IsSpecified);
        AddIfSpecified(fields, nameof(LegalIdentifier), LegalIdentifier.IsSpecified);
        AddIfSpecified(fields, nameof(Address), Address.IsSpecified);
        AddIfSpecified(fields, nameof(ContactDetails), ContactDetails.IsSpecified);
        AddIfSpecified(fields, nameof(SubscriptionPlanReference), SubscriptionPlanReference.IsSpecified);
        AddIfSpecified(fields, nameof(SubscriptionStatus), SubscriptionStatus.IsSpecified);
        AddIfSpecified(fields, nameof(SubscriptionStartedAt), SubscriptionStartedAt.IsSpecified);
        AddIfSpecified(fields, nameof(SubscriptionEndsAt), SubscriptionEndsAt.IsSpecified);
        return fields;
    }

    private static void AddIfSpecified(ISet<string> fields, string field, bool isSpecified)
    {
        if (isSpecified)
        {
            fields.Add(field);
        }
    }
}
