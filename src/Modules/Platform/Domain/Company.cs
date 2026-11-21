using System.Globalization;
using System.Net.Mail;

namespace Elangi.Modules.Platform.Domain;

public sealed record CompanyContactDetails(string? Email, string? Phone);

public sealed record Company
{
    private Company(
        Guid id,
        string legalName,
        string? tradeName,
        string countryCode,
        string? legalIdentifier,
        string? address,
        CompanyContactDetails contactDetails,
        CompanyStatus status,
        string? subscriptionPlanReference,
        string? subscriptionStatus,
        DateTimeOffset? subscriptionStartedAt,
        DateTimeOffset? subscriptionEndsAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        LegalName = legalName;
        TradeName = tradeName;
        CountryCode = countryCode;
        LegalIdentifier = legalIdentifier;
        Address = address;
        ContactDetails = contactDetails;
        Status = status;
        SubscriptionPlanReference = subscriptionPlanReference;
        SubscriptionStatus = subscriptionStatus;
        SubscriptionStartedAt = subscriptionStartedAt;
        SubscriptionEndsAt = subscriptionEndsAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }
    public string LegalName { get; }
    public string? TradeName { get; }
    public string CountryCode { get; }
    public string? LegalIdentifier { get; }
    public string? Address { get; }
    public CompanyContactDetails ContactDetails { get; }
    public CompanyStatus Status { get; }
    public string? SubscriptionPlanReference { get; }
    public string? SubscriptionStatus { get; }
    public DateTimeOffset? SubscriptionStartedAt { get; }
    public DateTimeOffset? SubscriptionEndsAt { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static Company Create(
        string legalName,
        string? tradeName,
        string countryCode,
        string? legalIdentifier,
        string? address,
        CompanyContactDetails? contactDetails)
    {
        var now = DateTimeOffset.UtcNow;
        return new Company(
            Guid.NewGuid(),
            Required(legalName, nameof(legalName)),
            Optional(tradeName),
            NormalizeCountry(countryCode),
            Optional(legalIdentifier),
            Optional(address),
            ValidateContact(contactDetails ?? new CompanyContactDetails(null, null)),
            CompanyStatus.Suspended,
            null,
            null,
            null,
            null,
            now,
            now);
    }

    internal static Company Restore(
        Guid id,
        string legalName,
        string? tradeName,
        string countryCode,
        string? legalIdentifier,
        string? address,
        CompanyContactDetails contactDetails,
        CompanyStatus status,
        string? subscriptionPlanReference,
        string? subscriptionStatus,
        DateTimeOffset? subscriptionStartedAt,
        DateTimeOffset? subscriptionEndsAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(
            id,
            legalName,
            tradeName,
            countryCode,
            legalIdentifier,
            address,
            contactDetails,
            status,
            subscriptionPlanReference,
            subscriptionStatus,
            subscriptionStartedAt,
            subscriptionEndsAt,
            createdAt,
            updatedAt);

    public Company WithIdentity(
        string legalName,
        string? tradeName,
        string countryCode,
        string? legalIdentifier,
        string? address,
        CompanyContactDetails? contactDetails) =>
        new(
            Id,
            Required(legalName, nameof(legalName)),
            Optional(tradeName),
            NormalizeCountry(countryCode),
            Optional(legalIdentifier),
            Optional(address),
            ValidateContact(contactDetails ?? new CompanyContactDetails(null, null)),
            Status,
            SubscriptionPlanReference,
            SubscriptionStatus,
            SubscriptionStartedAt,
            SubscriptionEndsAt,
            CreatedAt,
            UpdatedAt);

    public Company WithSubscription(
        string? planReference,
        string? status,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
    {
        if (startsAt is not null && endsAt is not null && endsAt < startsAt)
        {
            throw new ArgumentException("Subscription end must not be before its start.");
        }

        return new Company(
            Id,
            LegalName,
            TradeName,
            CountryCode,
            LegalIdentifier,
            Address,
            ContactDetails,
            Status,
            Optional(planReference),
            Optional(status),
            startsAt,
            endsAt,
            CreatedAt,
            UpdatedAt);
    }

    public Company WithStatus(CompanyStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new Company(
            Id,
            LegalName,
            TradeName,
            CountryCode,
            LegalIdentifier,
            Address,
            ContactDetails,
            status,
            SubscriptionPlanReference,
            SubscriptionStatus,
            SubscriptionStartedAt,
            SubscriptionEndsAt,
            CreatedAt,
            DateTimeOffset.UtcNow);
    }

    private static CompanyContactDetails ValidateContact(CompanyContactDetails details)
    {
        var email = Optional(details.Email);
        if (email is not null)
        {
            try
            {
                _ = new MailAddress(email);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("Contact email is invalid.", nameof(details), exception);
            }
        }

        return new CompanyContactDetails(email, Optional(details.Phone));
    }

    private static string Required(string? value, string parameter)
    {
        var normalized = Optional(value);
        return normalized ?? throw new ArgumentException("A value is required.", parameter);
    }

    private static string? Optional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string NormalizeCountry(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || normalized.Length != 2 || normalized.Any(character => !char.IsAsciiLetter(character)))
        {
            throw new ArgumentException(
                "Country code must be a two-letter ISO code.",
                nameof(value));
        }

        try
        {
            return new RegionInfo(normalized).TwoLetterISORegionName;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "Country code must identify a supported ISO 3166-1 region.",
                nameof(value),
                exception);
        }
    }
}

public enum CompanyStatus
{
    Active,
    Suspended
}
