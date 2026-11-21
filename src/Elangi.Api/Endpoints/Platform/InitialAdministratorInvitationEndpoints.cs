using Elangi.Modules.Platform.Application;

namespace Elangi.Api.Endpoints.Platform;

public static class InitialAdministratorInvitationEndpoints
{
    public static IEndpointRouteBuilder MapInitialAdministratorInvitationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/platform/companies/{companyId:guid}/initial-administrator-invitations")
            .RequireAuthorization("PlatformOwner");

        group.MapPost("", async (
            Guid companyId,
            CreateInitialAdministratorInvitationRequest request,
            InitialAdministratorInvitationService service,
            CancellationToken cancellationToken) =>
        {
            var invitation = await service.InviteAsync(
                companyId,
                request.Email,
                cancellationToken);

            return Results.Created(
                $"/api/v1/platform/companies/{companyId}/initial-administrator-invitations/{invitation.Id}",
                InitialAdministratorInvitationResponse.From(invitation));
        });

        return endpoints;
    }
}

public sealed record CreateInitialAdministratorInvitationRequest(string Email);

public sealed record InitialAdministratorInvitationResponse(
    Guid Id,
    Guid CompanyId,
    string InviteeEmail,
    string Status,
    DateTimeOffset ExpiresAt)
{
    public static InitialAdministratorInvitationResponse From(
        InitialAdministratorInvitation invitation) =>
        new(
            invitation.Id,
            invitation.CompanyId,
            invitation.InviteeEmail,
            "Pending",
            invitation.ExpiresAt);
}
