using System.Security.Claims;
using Elangi.Modules.Security.Application;

namespace Elangi.Api.Authentication;

public sealed class CurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var subject = principal?.FindFirstValue("sub");

            if (principal?.Identity?.IsAuthenticated != true ||
                !Guid.TryParse(subject, out var userId))
            {
                throw new UnauthorizedAccessException("The authenticated Supabase subject is unavailable.");
            }

            return userId;
        }
    }
}
