using Elangi.Modules.Platform.Domain;
using Elangi.Modules.Security.Application;
using CompanyStatus = Elangi.Modules.Platform.Domain.CompanyStatus;

namespace Elangi.Modules.Platform.Application;

public sealed class CompanyAdministrationService(
    ICurrentActor currentActor,
    PlatformAuthorizationService platformAuthorization,
    IPlatformCompanyRepository repository)
{
    public async Task<Company> CreateAsync(
        CompanyContactDetails? contactDetails,
        string legalName,
        string? tradeName,
        string countryCode,
        string? legalIdentifier,
        string? address,
        string? subscriptionPlanReference,
        string? subscriptionStatus,
        DateTimeOffset? subscriptionStartedAt,
        DateTimeOffset? subscriptionEndsAt,
        CancellationToken cancellationToken)
    {
        var actorProfileId =
            await platformAuthorization.RequirePlatformOwnerAsync(cancellationToken);
        var company = Company.Create(
            legalName,
            tradeName,
            countryCode,
            legalIdentifier,
            address,
            contactDetails).WithSubscription(
            subscriptionPlanReference,
            subscriptionStatus,
            subscriptionStartedAt,
            subscriptionEndsAt);

        return await repository.CreateAsync(
            company,
            currentActor.UserId,
            actorProfileId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken)
    {
        await platformAuthorization.RequirePlatformOwnerAsync(cancellationToken);
        return await repository.ListAsync(currentActor.UserId, cancellationToken);
    }

    public async Task<Company?> FindAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await platformAuthorization.RequirePlatformOwnerAsync(cancellationToken);
        return await repository.FindAsync(companyId, currentActor.UserId, cancellationToken);
    }

    public async Task<Company?> UpdateAsync(
        Guid companyId,
        CompanyUpdate update,
        CancellationToken cancellationToken)
    {
        var actorProfileId =
            await platformAuthorization.RequirePlatformOwnerAsync(cancellationToken);
        var current = await repository.FindAsync(companyId, currentActor.UserId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        if (!update.HasIdentityChanges && !update.HasSubscriptionChanges)
        {
            return current;
        }

        var updated = current;
        if (update.HasIdentityChanges)
        {
            updated = updated.WithIdentity(
                ValueOrCurrent(update.LegalName, updated.LegalName)
                    ?? throw new ArgumentException("Legal name cannot be cleared.", nameof(update)),
                ValueOrCurrent(update.TradeName, updated.TradeName),
                ValueOrCurrent(update.CountryCode, updated.CountryCode)
                    ?? throw new ArgumentException("Country code cannot be cleared.", nameof(update)),
                ValueOrCurrent(update.LegalIdentifier, updated.LegalIdentifier),
                ValueOrCurrent(update.Address, updated.Address),
                ValueOrCurrent(update.ContactDetails, updated.ContactDetails));
        }

        if (update.HasSubscriptionChanges)
        {
            updated = updated.WithSubscription(
                ValueOrCurrent(update.SubscriptionPlanReference, updated.SubscriptionPlanReference),
                ValueOrCurrent(update.SubscriptionStatus, updated.SubscriptionStatus),
                ValueOrCurrent(update.SubscriptionStartedAt, updated.SubscriptionStartedAt),
                ValueOrCurrent(update.SubscriptionEndsAt, updated.SubscriptionEndsAt));
        }

        return await repository.UpdateAsync(
            updated,
            update.ChangedFields(),
            currentActor.UserId,
            actorProfileId,
            cancellationToken);
    }

    public async Task<Company?> ChangeStatusAsync(
        Guid companyId,
        CompanyStatus status,
        CancellationToken cancellationToken)
    {
        var actorProfileId =
            await platformAuthorization.RequirePlatformOwnerAsync(cancellationToken);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return await repository.ChangeStatusAsync(
            companyId,
            status,
            currentActor.UserId,
            actorProfileId,
            cancellationToken);
    }

    private static T ValueOrCurrent<T>(OptionalField<T> update, T current) =>
        update.IsSpecified ? update.Value : current;
}
