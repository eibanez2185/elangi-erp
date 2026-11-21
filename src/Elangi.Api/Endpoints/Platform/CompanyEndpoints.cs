using System.Text.Json;
using Elangi.Api.Authorization;
using Elangi.Modules.Platform.Application;
using Elangi.Modules.Platform.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Elangi.Api.Endpoints.Platform;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapPlatformCompanyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/platform/companies")
            .RequireAuthorization("PlatformOwner");

        group.MapPost("/", async (
            CreateCompanyRequest request,
            CompanyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var company = await service.CreateAsync(
                request.ContactDetails,
                request.LegalName,
                request.TradeName,
                request.CountryCode,
                request.LegalIdentifier,
                request.Address,
                request.SubscriptionPlanReference,
                request.SubscriptionStatus,
                request.SubscriptionStartedAt,
                request.SubscriptionEndsAt,
                cancellationToken);

            return Results.Created(
                $"/api/v1/platform/companies/{company.Id}",
                CompanyResponse.From(company));
        });

        group.MapGet("/", async (
            CompanyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var companies = await service.ListAsync(cancellationToken);
            return Results.Ok(companies.Select(CompanyResponse.From));
        });

        group.MapGet("/{companyId:guid}", async (
            Guid companyId,
            CompanyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var company = await service.FindAsync(companyId, cancellationToken);
            return company is null
                ? Results.NotFound()
                : Results.Ok(CompanyResponse.From(company));
        });

        group.MapPatch("/{companyId:guid}", async (
            Guid companyId,
            JsonElement body,
            CompanyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The update body must be a JSON object.");
            }

            var update = new CompanyUpdate(
                ReadField(body, "legalName", ReadNullableString),
                ReadField(body, "tradeName", ReadNullableString),
                ReadField(body, "countryCode", ReadNullableString),
                ReadField(body, "legalIdentifier", ReadNullableString),
                ReadField(body, "address", ReadNullableString),
                ReadField(body, "contactDetails", ReadContactDetails),
                ReadField(body, "subscriptionPlanReference", ReadNullableString),
                ReadField(body, "subscriptionStatus", ReadNullableString),
                ReadField(body, "subscriptionStartedAt", ReadNullableTimestamp),
                ReadField(body, "subscriptionEndsAt", ReadNullableTimestamp));

            var company = await service.UpdateAsync(companyId, update, cancellationToken);
            return company is null
                ? Results.NotFound()
                : Results.Ok(CompanyResponse.From(company));
        });

        group.MapPut("/{companyId:guid}/status", async (
            Guid companyId,
            ChangeCompanyStatusRequest request,
            CompanyAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<CompanyStatus>(request.Status, ignoreCase: true, out var status) ||
                !Enum.IsDefined(status))
            {
                throw new ArgumentException("Status must be Active or Suspended.");
            }

            var company = await service.ChangeStatusAsync(companyId, status, cancellationToken);
            return company is null
                ? Results.NotFound()
                : Results.Ok(CompanyResponse.From(company));
        });

        return endpoints;
    }

    private static OptionalField<T> ReadField<T>(
        JsonElement body,
        string name,
        Func<JsonElement, T> read)
    {
        if (!body.TryGetProperty(name, out var value))
        {
            return new OptionalField<T>(false, default!);
        }

        return new OptionalField<T>(true, read(value));
    }

    private static string? ReadNullableString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            _ => throw new ArgumentException("A string or null value was expected.")
        };

    private static CompanyContactDetails? ReadContactDetails(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Contact details must be an object or null.");
        }

        return value.Deserialize<CompanyContactDetails>()
            ?? throw new ArgumentException("Contact details are invalid.");
    }

    private static DateTimeOffset? ReadNullableTimestamp(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String ||
            !value.TryGetDateTimeOffset(out var timestamp))
        {
            throw new ArgumentException("A valid ISO 8601 timestamp or null value was expected.");
        }

        return timestamp;
    }
}

public sealed record CreateCompanyRequest(
    string LegalName,
    string CountryCode,
    string? TradeName,
    string? LegalIdentifier,
    string? Address,
    CompanyContactDetails? ContactDetails,
    string? SubscriptionPlanReference,
    string? SubscriptionStatus,
    DateTimeOffset? SubscriptionStartedAt,
    DateTimeOffset? SubscriptionEndsAt);

public sealed record ChangeCompanyStatusRequest(string Status);

public sealed record CompanyResponse(
    Guid Id,
    string LegalName,
    string? TradeName,
    string CountryCode,
    string? LegalIdentifier,
    string? Address,
    CompanyContactDetails ContactDetails,
    string Status,
    string? SubscriptionPlanReference,
    string? SubscriptionStatus,
    DateTimeOffset? SubscriptionStartedAt,
    DateTimeOffset? SubscriptionEndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CompanyResponse From(Company company) =>
        new(
            company.Id,
            company.LegalName,
            company.TradeName,
            company.CountryCode,
            company.LegalIdentifier,
            company.Address,
            company.ContactDetails,
            company.Status.ToString(),
            company.SubscriptionPlanReference,
            company.SubscriptionStatus,
            company.SubscriptionStartedAt,
            company.SubscriptionEndsAt,
            company.CreatedAt,
            company.UpdatedAt);
}
