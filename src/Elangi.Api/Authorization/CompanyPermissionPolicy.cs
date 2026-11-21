using Elangi.Modules.Security.Application;
using Microsoft.AspNetCore.Authorization;

namespace Elangi.Api.Authorization;

public sealed record CompanyPermissionRequirement(string PermissionCode)
    : IAuthorizationRequirement;

public sealed class CompanyPermissionPolicy(
    CompanyAuthorizationService authorizationService)
    : AuthorizationHandler<CompanyPermissionRequirement, Guid>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CompanyPermissionRequirement requirement,
        Guid companyId)
    {
        try
        {
            await authorizationService.AuthorizeAsync(companyId, requirement.PermissionCode);
            context.Succeed(requirement);
        }
        catch (CompanyAccessDeniedException)
        {
            context.Fail();
        }
    }
}

public sealed class PlatformOwnerRequirement : IAuthorizationRequirement
{
}

public sealed class PlatformOwnerPolicy(
    PlatformAuthorizationService authorizationService)
    : AuthorizationHandler<PlatformOwnerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformOwnerRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        try
        {
            await authorizationService.RequirePlatformOwnerAsync();
            context.Succeed(requirement);
        }
        catch (PlatformAccessDeniedException)
        {
            context.Fail();
        }
    }
}
