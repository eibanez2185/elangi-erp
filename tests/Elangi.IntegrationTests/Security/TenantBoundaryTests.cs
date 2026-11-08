using Elangi.Shared.Infrastructure.Postgres;
using Elangi.Modules.Audit.Application;
using Elangi.Modules.Audit.Infrastructure;
using Npgsql;
using System.Text.Json;
using System.Net;

namespace Elangi.IntegrationTests.Security;

[Collection("Local Supabase")]
public sealed class TenantBoundaryTests
{
    [Fact]
    public async Task RuntimeRoleReadsOnlyAuditRowsForItsTransactionCompany()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE by dot-sourcing supabase/scripts/load-local-environment.ps1 after starting local Supabase.");
        }

        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var auditA = Guid.NewGuid();
        var auditB = Guid.NewGuid();

        await using var dataSource = CreateDataSource(connectionString);
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);
        await SeedAuditRowsAsync(dataSource, auditA, companyA, auditB, companyB);

        try
        {
            await using var tenantTransaction = await TenantTransaction.BeginAsync(
                dataSource,
                new TenantTransactionContext(actorId, companyA));
            await using var command = tenantTransaction.Connection.CreateCommand();
            command.Transaction = tenantTransaction.Transaction;
            command.CommandText = "SELECT count(*) FROM audit.audit_records WHERE id = ANY (@ids);";
            command.Parameters.AddWithValue("ids", new[] { auditA, auditB });

            var visibleCount = Convert.ToInt64(await command.ExecuteScalarAsync());
            Assert.Equal(1, visibleCount);

            await using var deniedWrite = tenantTransaction.Connection.CreateCommand();
            deniedWrite.Transaction = tenantTransaction.Transaction;
            deniedWrite.CommandText =
                """
                INSERT INTO audit.audit_records
                    (id, company_id, operation, occurred_at, target_type)
                VALUES
                    (@id, @foreign_company, 'test.write', now(), 'test');
                """;
            deniedWrite.Parameters.AddWithValue("id", Guid.NewGuid());
            deniedWrite.Parameters.AddWithValue("foreign_company", companyB);

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => deniedWrite.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }
        finally
        {
            await DeleteAuditRowsAsync(dataSource, auditA, auditB);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    [Fact]
    public async Task FoundationMigrationCreatesARestrictedRuntimeRoleAndForcesRls()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE by dot-sourcing supabase/scripts/load-local-environment.ps1 after starting local Supabase.");
        }

        await using var dataSource = CreateDataSource(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT role.rolsuper, role.rolbypassrls, role.rolcanlogin,
                   relation.relrowsecurity, relation.relforcerowsecurity,
                   relation.relowner = role.oid
            FROM pg_roles AS role
            CROSS JOIN pg_class AS relation
            WHERE role.rolname = 'elangi_runtime'
              AND relation.oid = 'audit.audit_records'::regclass;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
    }

    [Fact]
    public async Task AuditWriterPersistsInsideTheAuthorizedTenantTransaction()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE by dot-sourcing supabase/scripts/load-local-environment.ps1 after starting local Supabase.");
        }

        var companyId = Guid.NewGuid();
        var auditId = Guid.NewGuid();
        await using var dataSource = CreateDataSource(connectionString);
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);
        await using var tenantTransaction = await TenantTransaction.BeginAsync(
            dataSource,
            new TenantTransactionContext(Guid.NewGuid(), companyId));

        using var changeSummary = JsonDocument.Parse("""{"result":"verified"}""");
        var entry = new AuditEntry(
            companyId,
            null,
            "foundation.test",
            "test",
            auditId,
            changeSummary,
            DateTimeOffset.UtcNow);
        await new AuditWriter(new PostgresAuditRepository()).RecordAsync(
            entry,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        await tenantTransaction.CommitAsync();

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "SELECT operation, change_summary->>'result' FROM audit.audit_records WHERE target_id = @target_id;",
                connection);
            command.Parameters.AddWithValue("target_id", auditId);
            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("foundation.test", reader.GetString(0));
            Assert.Equal("verified", reader.GetString(1));
        }
        finally
        {
            await DeleteAuditByTargetAsync(dataSource, auditId);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    [Fact]
    public async Task RuntimeRoleCannotModifyOrDeleteExistingAuditRecords()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELANGI_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ELANGI_TEST_DATABASE by dot-sourcing supabase/scripts/load-local-environment.ps1 after starting local Supabase.");
        }

        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var auditId = Guid.NewGuid();
        var otherAuditId = Guid.NewGuid();
        await using var dataSource = CreateDataSource(connectionString);
        await GrantRuntimeRoleToCurrentUserAsync(dataSource);
        await SeedAuditRowsAsync(dataSource, auditId, companyId, otherAuditId, Guid.NewGuid());

        try
        {
            await AssertAuditMutationDeniedAsync(
                dataSource,
                actorId,
                companyId,
                "UPDATE audit.audit_records SET operation = 'tampered' WHERE id = @id;",
                auditId);
            await AssertAuditMutationDeniedAsync(
                dataSource,
                actorId,
                companyId,
                "DELETE FROM audit.audit_records WHERE id = @id;",
                auditId);
        }
        finally
        {
            await DeleteAuditRowsAsync(dataSource, auditId, otherAuditId);
            await RevokeRuntimeRoleFromCurrentUserAsync(dataSource);
        }
    }

    private static async Task SeedAuditRowsAsync(
        NpgsqlDataSource dataSource,
        Guid auditA,
        Guid companyA,
        Guid auditB,
        Guid companyB)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO audit.audit_records
                (id, company_id, operation, occurred_at, target_type)
            VALUES
                (@audit_a, @company_a, 'test.seed', now(), 'test'),
                (@audit_b, @company_b, 'test.seed', now(), 'test');
            """,
            connection);
        command.Parameters.AddWithValue("audit_a", auditA);
        command.Parameters.AddWithValue("company_a", companyA);
        command.Parameters.AddWithValue("audit_b", auditB);
        command.Parameters.AddWithValue("company_b", companyB);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteAuditRowsAsync(
        NpgsqlDataSource dataSource,
        Guid auditA,
        Guid auditB)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM audit.audit_records WHERE id = ANY (@ids);",
            connection);
        command.Parameters.AddWithValue("ids", new[] { auditA, auditB });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteAuditByTargetAsync(NpgsqlDataSource dataSource, Guid targetId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM audit.audit_records WHERE target_id = @target_id;",
            connection);
        command.Parameters.AddWithValue("target_id", targetId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertAuditMutationDeniedAsync(
        NpgsqlDataSource dataSource,
        Guid actorId,
        Guid companyId,
        string sql,
        Guid auditId)
    {
        await using var tenantTransaction = await TenantTransaction.BeginAsync(
            dataSource,
            new TenantTransactionContext(actorId, companyId));
        await using var command = tenantTransaction.Connection.CreateCommand();
        command.Transaction = tenantTransaction.Transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("id", auditId);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var databaseUri) ||
            databaseUri.Scheme is not ("postgres" or "postgresql"))
        {
            var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(connectionBuilder.Host))
            {
                throw new InvalidOperationException("ELANGI_TEST_DATABASE must include a local database host.");
            }

            EnsureLocalDatabase(connectionBuilder.Host, connectionBuilder.Port);
            return NpgsqlDataSource.Create(connectionBuilder.ConnectionString);
        }

        EnsureLocalDatabase(databaseUri.Host, databaseUri.Port);
        var credentials = databaseUri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("ELANGI_TEST_DATABASE must include local database credentials.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
            Database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1])
        };

        return NpgsqlDataSource.Create(builder.ConnectionString);
    }

    private static void EnsureLocalDatabase(string host, int port)
    {
        var isLoopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
        if (!isLoopback || port != 54322)
        {
            throw new InvalidOperationException(
                "Integration tests are restricted to the local Supabase database at port 54322.");
        }
    }

    private static async Task GrantRuntimeRoleToCurrentUserAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "GRANT elangi_runtime TO CURRENT_USER;",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RevokeRuntimeRoleFromCurrentUserAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "REVOKE elangi_runtime FROM CURRENT_USER;",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
