using System.Security.Claims;
using Elangi.Api.Authentication;
using Microsoft.AspNetCore.Http;

namespace Elangi.UnitTests.Authentication;

public sealed class CurrentActorTests
{
    [Fact]
    public void UserId_UsesTheVerifiedSupabaseSubject()
    {
        var expectedUserId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", expectedUserId.ToString())],
                authenticationType: "Bearer"))
        };
        var actor = new CurrentActor(new HttpContextAccessor { HttpContext = httpContext });

        Assert.Equal(expectedUserId, actor.UserId);
    }

    [Fact]
    public void UserId_RejectsUnauthenticatedOrInvalidSubjects()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "not-a-uuid")]))
        };
        var actor = new CurrentActor(new HttpContextAccessor { HttpContext = httpContext });

        Assert.Throws<UnauthorizedAccessException>(() => actor.UserId);
    }
}
