using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Elangi.Modules.Platform.Application;
using Elangi.Modules.Security.Infrastructure;
using Elangi.Shared.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Elangi.ContractTests.Platform;

[Collection("Platform company API")]
public sealed class CompanyAdministrationContractTests(PlatformApiFactory factory) : IAsyncLifetime
{
    [Fact]
    public async Task PlatformOwnerCanCreateReadUpdateChangeStatusAndInviteAnAdministrator()
    {
        var owner = await factory.CreateActorAsync(isPlatformOwner: true);
        using var client = factory.CreateAuthenticatedClient(owner.AuthUserId);
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/platform/companies",
            new
            {
                legalName = "Example Legal Name",
                tradeName = "Example Trade Name",
                countryCode = "EC",
                legalIdentifier = "1790000000001",
                address = "Quito",
                contactDetails = new { email = "office@example.test", phone = "+59320000000" },
                subscriptionPlanReference = "standard",
                subscriptionStatus = "trial"
            });

        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"{createResponse.Headers.WwwAuthenticate}: {await createResponse.Content.ReadAsStringAsync()}");
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var companyId = created.RootElement.GetProperty("id").GetGuid();
        factory.TrackCompany(companyId);
        Assert.Equal("Suspended", created.RootElement.GetProperty("status").GetString());
        Assert.Equal("Example Legal Name", created.RootElement.GetProperty("legalName").GetString());
        Assert.Equal("standard", created.RootElement.GetProperty("subscriptionPlanReference").GetString());

        var listResponse = await client.GetAsync("/api/v1/platform/companies");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            list.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == companyId);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/platform/companies/{companyId}",
            new
            {
                tradeName = "Updated Trade Name",
                subscriptionStatus = "active"
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var activateResponse = await client.PutAsJsonAsync(
            $"/api/v1/platform/companies/{companyId}/status",
            new { status = "Active" });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        using var active = JsonDocument.Parse(await activateResponse.Content.ReadAsStringAsync());
        Assert.Equal("Active", active.RootElement.GetProperty("status").GetString());

        var suspendResponse = await client.PutAsJsonAsync(
            $"/api/v1/platform/companies/{companyId}/status",
            new { status = "Suspended" });
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);
        using var suspended = JsonDocument.Parse(await suspendResponse.Content.ReadAsStringAsync());
        Assert.Equal("Suspended", suspended.RootElement.GetProperty("status").GetString());

        var detailResponse = await client.GetAsync($"/api/v1/platform/companies/{companyId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.Equal("Updated Trade Name", detail.RootElement.GetProperty("tradeName").GetString());
        Assert.Equal("active", detail.RootElement.GetProperty("subscriptionStatus").GetString());
        Assert.Equal("Suspended", detail.RootElement.GetProperty("status").GetString());

        var invitationResponse = await client.PostAsJsonAsync(
            $"/api/v1/platform/companies/{companyId}/initial-administrator-invitations",
            new { email = "initial-admin@example.test" });
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitationBody = await invitationResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", invitationBody, StringComparison.OrdinalIgnoreCase);
        using var invitation = JsonDocument.Parse(invitationBody);
        Assert.Equal(
            "initial-admin@example.test",
            invitation.RootElement.GetProperty("inviteeEmail").GetString());
        Assert.Equal("Pending", invitation.RootElement.GetProperty("status").GetString());

        var email = Assert.Single(factory.EmailSender.Messages);
        Assert.Equal("initial-admin@example.test", email.Recipient);
        Assert.Contains($"companyId={companyId}", email.Link, StringComparison.Ordinal);
        var secret = ReadQueryParameter(email.Link, "token");
        Assert.NotEmpty(secret);

        await using var connection = await factory.DataSource.OpenConnectionAsync();
        await using var verification = new NpgsqlCommand(
            """
            SELECT company.status,
                   membership.status,
                   membership.user_profile_id IS NULL,
                   invitation.token_digest,
                   invitation.created_by_platform_profile_id
            FROM platform.companies AS company
            JOIN security.memberships AS membership
                ON membership.company_id = company.id
            JOIN security.invitations AS invitation
                ON invitation.company_id = membership.company_id
               AND invitation.membership_id = membership.id
            WHERE company.id = @company_id;
            """,
            connection);
        verification.Parameters.AddWithValue("company_id", companyId);
        await using var reader = await verification.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Suspended", reader.GetString(0));
        Assert.Equal("Pending", reader.GetString(1));
        Assert.True(reader.GetBoolean(2));
        Assert.NotEqual(secret, reader.GetString(3));
        Assert.Equal(owner.ProfileId, reader.GetGuid(4));
        await reader.CloseAsync();

        await using var auditCommand = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM audit.audit_records
            WHERE company_id IS NULL
              AND target_id = @company_id
              AND operation LIKE 'platform.company.%';
            """,
            connection);
        auditCommand.Parameters.AddWithValue("company_id", companyId);
        Assert.Equal(4L, (long)(await auditCommand.ExecuteScalarAsync())!);

        var accessReader = new PostgresCompanyAccessReader(
            new TenantTransactionFactory(factory.DataSource));
        var companyAccess = await accessReader.FindAsync(
            owner.AuthUserId,
            companyId,
            CancellationToken.None);
        Assert.Null(companyAccess);
    }

    [Fact]
    public async Task NonPlatformOwnerCannotAdministerCompaniesAndUnauthenticatedRequestIsRejected()
    {
        var user = await factory.CreateActorAsync(isPlatformOwner: false);
        using var client = factory.CreateAuthenticatedClient(user.AuthUserId);

        var listResponse = await client.GetAsync("/api/v1/platform/companies");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/platform/companies",
            new { legalName = "Unauthorized", countryCode = "US" });
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        using var anonymousClient = factory.CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/v1/platform/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task FailedInvitationDeliveryRevokesPendingInvitationAndMembership()
    {
        var owner = await factory.CreateActorAsync(isPlatformOwner: true);
        using var client = factory.CreateAuthenticatedClient(owner.AuthUserId);
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/platform/companies",
            new { legalName = "Delivery Failure Company", countryCode = "US" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var company = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var companyId = company.RootElement.GetProperty("id").GetGuid();
        factory.TrackCompany(companyId);
        factory.EmailSender.Failure =
            new InvitationEmailDeliveryException("Test SMTP delivery failure.");

        var invitationResponse = await client.PostAsJsonAsync(
            $"/api/v1/platform/companies/{companyId}/initial-administrator-invitations",
            new { email = "failed-admin@example.test" });

        Assert.Equal(HttpStatusCode.InternalServerError, invitationResponse.StatusCode);
        await using var connection = await factory.DataSource.OpenConnectionAsync();
        await using var stateCommand = new NpgsqlCommand(
            """
            SELECT invitation.status, membership.status
            FROM security.invitations AS invitation
            JOIN security.memberships AS membership
              ON membership.company_id = invitation.company_id
             AND membership.id = invitation.membership_id
            WHERE invitation.company_id = @company_id;
            """,
            connection);
        stateCommand.Parameters.AddWithValue("company_id", companyId);
        await using var reader = await stateCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Revoked", reader.GetString(0));
        Assert.Equal("Revoked", reader.GetString(1));
    }

    public async Task InitializeAsync()
    {
        await factory.ResetEmailMessagesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string ReadQueryParameter(string uri, string name)
    {
        var fragment = new Uri(uri).Fragment.TrimStart('#');
        var queryStart = fragment.IndexOf('?');
        if (queryStart < 0)
        {
            return string.Empty;
        }

        var query = fragment[(queryStart + 1)..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                Uri.UnescapeDataString(parts[0]).Equals(name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return string.Empty;
    }
}

[CollectionDefinition("Platform company API", DisableParallelization = true)]
public sealed class PlatformCompanyApiCollection : ICollectionFixture<PlatformApiFactory>
{
}

public sealed class PlatformApiFactory : WebApplicationFactory<Program>
{
    private const string SigningSecret = "platform-api-contract-test-secret-with-32-bytes";
    private readonly Dictionary<Guid, Guid> testActors = new();
    private readonly HashSet<Guid> testCompanies = [];
    private readonly string connectionString = GetLocalDatabaseConnectionString();
    private readonly NpgsqlDataSource dataSource;
    private bool runtimeRoleGranted;
    private bool disposed;

    public PlatformApiFactory()
    {
        dataSource = NpgsqlDataSource.Create(connectionString);
        EmailSender = new CapturingInvitationEmailSender();
    }

    public CapturingInvitationEmailSender EmailSender { get; }
    public NpgsqlDataSource DataSource => dataSource;

    public void TrackCompany(Guid companyId) => testCompanies.Add(companyId);

    public async Task<(Guid AuthUserId, Guid ProfileId)> CreateActorAsync(bool isPlatformOwner)
    {
        await EnsureRuntimeRoleGrantedAsync();
        var authUserId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO auth.users (id, email, created_at, updated_at)
            VALUES (@auth_user_id, @email, now(), now());

            INSERT INTO security.user_profiles (id, auth_user_id, display_name)
            VALUES (@profile_id, @auth_user_id, 'Contract test actor');
            """,
            connection);
        command.Parameters.AddWithValue("auth_user_id", authUserId);
        command.Parameters.AddWithValue("email", $"{authUserId:N}@contract.invalid");
        command.Parameters.AddWithValue("profile_id", profileId);
        await command.ExecuteNonQueryAsync();

        if (isPlatformOwner)
        {
            await using var roleCommand = new NpgsqlCommand(
                """
                INSERT INTO platform.user_platform_roles (user_profile_id, role_id)
                SELECT @profile_id, id
                FROM platform.platform_roles
                WHERE code = 'platform-owner' AND is_active;
                """,
                connection);
            roleCommand.Parameters.AddWithValue("profile_id", profileId);
            Assert.Equal(1, await roleCommand.ExecuteNonQueryAsync());
        }

        testActors.Add(authUserId, profileId);
        return (authUserId, profileId);
    }

    public HttpClient CreateAuthenticatedClient(Guid authUserId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(authUserId));
        return client;
    }

    public Task ResetEmailMessagesAsync()
    {
        EmailSender.Clear();
        EmailSender.Failure = null;
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ApplicationDatabase", connectionString);
        builder.UseSetting("SUPABASE_JWT_ISSUER", "https://contract-test.supabase.invalid/auth/v1");
        builder.UseSetting("SUPABASE_JWT_AUDIENCE", "authenticated");
        builder.UseSetting("SUPABASE_JWT_SECRET", SigningSecret);
        builder.UseSetting("SUPABASE_JWT_USE_JWKS", "false");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SUPABASE_JWT_ISSUER"] = "https://contract-test.supabase.invalid/auth/v1",
                ["SUPABASE_JWT_AUDIENCE"] = "authenticated",
                ["SUPABASE_JWT_SECRET"] = SigningSecret,
                ["SUPABASE_JWT_USE_JWKS"] = "false",
                ["ConnectionStrings:ApplicationDatabase"] = connectionString,
                ["InvitationEmail:SmtpHost"] = "127.0.0.1",
                ["InvitationEmail:SmtpPort"] = "54325",
                ["InvitationEmail:FromAddress"] = "no-reply@example.test",
                ["InvitationEmail:FrontendBaseUrl"] = "http://127.0.0.1:5173/",
                ["InvitationEmail:UseSsl"] = "false"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvitationEmailSender>();
            services.AddSingleton<IInvitationEmailSender>(EmailSender);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            CleanupTestActorsAsync().GetAwaiter().GetResult();
            if (runtimeRoleGranted)
            {
                using var connection = dataSource.OpenConnection();
                using var command = new NpgsqlCommand("REVOKE elangi_runtime FROM CURRENT_USER;", connection);
                command.ExecuteNonQuery();
            }

            dataSource.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task EnsureRuntimeRoleGrantedAsync()
    {
        if (runtimeRoleGranted)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("GRANT elangi_runtime TO CURRENT_USER;", connection);
        await command.ExecuteNonQueryAsync();
        runtimeRoleGranted = true;
    }

    private async Task CleanupTestActorsAsync()
    {
        if (testActors.Count == 0 && testCompanies.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM platform.user_platform_roles
            WHERE user_profile_id = ANY (
                SELECT id FROM security.user_profiles WHERE auth_user_id = ANY (@actor_ids)
            );
            DELETE FROM security.invitations
            WHERE company_id = ANY (@company_ids)
               OR created_by_platform_profile_id = ANY (
                    SELECT id FROM security.user_profiles WHERE auth_user_id = ANY (@actor_ids)
               );
            DELETE FROM security.company_role_permissions
            WHERE company_id = ANY (@company_ids);
            DELETE FROM security.membership_roles
            WHERE company_id = ANY (@company_ids);
            DELETE FROM security.memberships
            WHERE company_id = ANY (@company_ids);
            DELETE FROM security.company_roles
            WHERE company_id = ANY (@company_ids);
            DELETE FROM audit.audit_records
            WHERE target_id = ANY (@company_ids);
            DELETE FROM platform.companies WHERE id = ANY (@company_ids);
            DELETE FROM auth.users WHERE id = ANY (@actor_ids);
            """,
            connection);
        command.Parameters.AddWithValue("actor_ids", testActors.Keys.ToArray());
        command.Parameters.AddWithValue("company_ids", testCompanies.ToArray());
        await command.ExecuteNonQueryAsync();
        testActors.Clear();
        testCompanies.Clear();
    }

    private static string CreateAccessToken(Guid authUserId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "https://contract-test.supabase.invalid/auth/v1",
            audience: "authenticated",
            claims: [new Claim("sub", authUserId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GetLocalDatabaseConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE by dot-sourcing supabase/scripts/load-local-environment.ps1.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var databaseUri) ||
            databaseUri.Scheme is not ("postgres" or "postgresql"))
        {
            var builder = new NpgsqlConnectionStringBuilder(value);
            if (!IsLocalDatabase(builder.Host ?? string.Empty, builder.Port))
            {
                throw new InvalidOperationException(
                    "Contract tests are restricted to the local Supabase database at port 54322.");
            }

            return builder.ConnectionString;
        }

        var port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port;
        if (!IsLocalDatabase(databaseUri.Host, port))
        {
            throw new InvalidOperationException(
                "Contract tests are restricted to the local Supabase database at port 54322.");
        }

        var credentials = databaseUri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("ELANGI_TEST_DATABASE must include local database credentials.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = port,
            Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        }.ConnectionString;
    }

    private static bool IsLocalDatabase(string host, int port) =>
        (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         System.Net.IPAddress.TryParse(host, out var address) &&
         System.Net.IPAddress.IsLoopback(address)) && port == 54322;
}

public sealed class CapturingInvitationEmailSender : IInvitationEmailSender
{
    private readonly List<CapturedInvitationEmail> messages = [];

    public IReadOnlyList<CapturedInvitationEmail> Messages => messages;
    public Exception? Failure { get; set; }

    public Task SendAsync(
        string recipient,
        string companyName,
        Guid companyId,
        Guid invitationId,
        string originalToken,
        CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        var invitationUrl =
            $"http://127.0.0.1:5173/#accept-invitation?companyId={companyId}" +
            $"&invitationId={invitationId}&token={Uri.EscapeDataString(originalToken)}";
        messages.Add(new CapturedInvitationEmail(recipient, companyName, invitationUrl));
        return Task.CompletedTask;
    }

    public void Clear() => messages.Clear();
}

public sealed record CapturedInvitationEmail(string Recipient, string CompanyName, string Link);
