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
