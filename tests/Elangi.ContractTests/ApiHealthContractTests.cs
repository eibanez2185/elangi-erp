using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Elangi.ContractTests;

public sealed class ApiHealthContractTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public ApiHealthContractTests(ApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_IsAvailableWithoutUserAuthentication()
    {
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync());
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> originalValues = new();

    public ApiFactory()
    {
        SetEnvironmentValue("SUPABASE_JWT_ISSUER", "http://localhost:54321/auth/v1");
        SetEnvironmentValue("SUPABASE_JWT_SECRET", "contract-test-signing-secret-not-a-real-credential");
        SetEnvironmentValue("SUPABASE_JWT_USE_JWKS", "false");
        SetEnvironmentValue("ConnectionStrings__ApplicationDatabase",
            "Host=127.0.0.1;Port=54322;Database=postgres;Username=elangi_runtime;Password=not-used-for-health-test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var (name, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private void SetEnvironmentValue(string name, string value)
    {
        originalValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}
