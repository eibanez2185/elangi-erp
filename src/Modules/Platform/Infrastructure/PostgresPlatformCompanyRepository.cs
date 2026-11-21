using System.Data;
using System.Text.Json;
using Elangi.Modules.Audit.Application;
using Elangi.Modules.Platform.Application;
using Elangi.Modules.Platform.Domain;
using Elangi.Shared.Infrastructure.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace Elangi.Modules.Platform.Infrastructure;

public sealed class PostgresPlatformCompanyRepository(
    TenantTransactionFactory transactionFactory,
    AuditWriter auditWriter) : IPlatformCompanyRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Company> CreateAsync(
        Company company,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO platform.companies
                (id, legal_name, trade_name, country_code, legal_identifier, address,
                 contact_details, status, subscription_plan_reference, subscription_status,
                 subscription_started_at, subscription_ends_at)
            VALUES
                (@id, @legal_name, @trade_name, @country_code, @legal_identifier, @address,
                 @contact_details, 'Suspended', @subscription_plan_reference, @subscription_status,
                 @subscription_started_at, @subscription_ends_at);
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction))
        {
            AddCompanyParameters(command, company);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var roleCommand = new NpgsqlCommand(
            """
            INSERT INTO security.company_roles (id, company_id, name, is_active)
            VALUES (@role_id, @company_id, 'Company Administrator', true);
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction))
        {
            roleCommand.Parameters.AddWithValue("role_id", Guid.NewGuid());
            roleCommand.Parameters.AddWithValue("company_id", company.Id);
            await roleCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecordAuditAsync(
            tenantTransaction,
            actorProfileId,
            "platform.company.created",
            "Company",
            company.Id,
            new { status = CompanyStatus.Suspended.ToString() },
            cancellationToken);

        var created = await FindCompanyAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            company.Id,
            cancellationToken);
        await tenantTransaction.CommitAsync(cancellationToken);

        return created ?? throw new InvalidOperationException(
            "The created company was not visible inside its transaction.");
    }

    public async Task<IReadOnlyList<Company>> ListAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT id, legal_name, trade_name, country_code, legal_identifier, address,
                   contact_details, status, subscription_plan_reference, subscription_status,
                   subscription_started_at, subscription_ends_at, created_at, updated_at
            FROM platform.companies
            ORDER BY created_at DESC, id DESC;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var companies = new List<Company>();
        while (await reader.ReadAsync(cancellationToken))
        {
            companies.Add(ReadCompany(reader));
        }

        await reader.CloseAsync();
        await tenantTransaction.CommitAsync(cancellationToken);
        return companies;
    }

    public async Task<Company?> FindAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        var company = await FindCompanyAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            companyId,
            cancellationToken);
        await tenantTransaction.CommitAsync(cancellationToken);
        return company;
    }

    public async Task<Company?> UpdateAsync(
        Company company,
        IReadOnlySet<string> changedFields,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE platform.companies
            SET legal_name = @legal_name,
                trade_name = @trade_name,
                country_code = @country_code,
                legal_identifier = @legal_identifier,
                address = @address,
                contact_details = @contact_details,
                subscription_plan_reference = @subscription_plan_reference,
                subscription_status = @subscription_status,
                subscription_started_at = @subscription_started_at,
                subscription_ends_at = @subscription_ends_at,
                updated_at = now()
            WHERE id = @id AND updated_at = @expected_updated_at;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        AddCompanyParameters(command, company);
        command.Parameters.AddWithValue("expected_updated_at", company.UpdatedAt);

        var updatedCount = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updatedCount == 0)
        {
            await tenantTransaction.DisposeAsync();
            var current = await FindAsync(company.Id, actorUserId, cancellationToken);
            if (current is null)
            {
                return null;
            }

            throw new PlatformConflictException(
                "The company changed while the update was being applied. Reload and retry.");
        }

        await RecordAuditAsync(
            tenantTransaction,
            actorProfileId,
            "platform.company.updated",
            "Company",
            company.Id,
            new { changedFields = changedFields.Order(StringComparer.Ordinal).ToArray() },
            cancellationToken);

        var updated = await FindCompanyAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            company.Id,
            cancellationToken);
        await tenantTransaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<Company?> ChangeStatusAsync(
        Guid companyId,
        CompanyStatus status,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        var current = await FindCompanyAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            companyId,
            cancellationToken,
            forUpdate: true);
        if (current is null)
        {
            await tenantTransaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current.Status == status)
        {
            await tenantTransaction.CommitAsync(cancellationToken);
            return current;
        }

        await using var command = new NpgsqlCommand(
            """
            UPDATE platform.companies
            SET status = @status, updated_at = now()
            WHERE id = @company_id;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("company_id", companyId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await RecordAuditAsync(
            tenantTransaction,
            actorProfileId,
            "platform.company.status.updated",
            "Company",
            companyId,
            new { previousStatus = current.Status.ToString(), status = status.ToString() },
            cancellationToken);

        var updated = await FindCompanyAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            companyId,
            cancellationToken);
        await tenantTransaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<InitialAdministratorInvitation> CreateInitialAdministratorInvitationAsync(
        Guid companyId,
        string inviteeEmail,
        string tokenDigest,
        DateTimeOffset expiresAt,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        var company = await FindCompanyAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            companyId,
            cancellationToken,
            forUpdate: true);
        if (company is null)
        {
            throw new PlatformResourceNotFoundException("Company was not found.");
        }

        var roleId = await FindInitialAdministratorRoleIdAsync(
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            companyId,
            cancellationToken);
        if (roleId is null)
        {
            throw new InvalidOperationException(
                "The company does not have its initial Company Administrator role.");
        }

        if (await HasInitialAdministratorInvitationAsync(
                tenantTransaction.Connection,
                tenantTransaction.Transaction,
                companyId,
                roleId.Value,
                cancellationToken))
        {
            throw new PlatformConflictException(
                "The initial Company Administrator has already been invited.");
        }

        var invitationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        await using (var membershipCommand = new NpgsqlCommand(
            """
            INSERT INTO security.memberships
                (id, company_id, user_profile_id, status, is_default_company)
            VALUES
                (@membership_id, @company_id, NULL, 'Pending', false);
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction))
        {
            membershipCommand.Parameters.AddWithValue("membership_id", membershipId);
            membershipCommand.Parameters.AddWithValue("company_id", companyId);
            await membershipCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var invitationCommand = new NpgsqlCommand(
            """
            INSERT INTO security.invitations
                (id, company_id, membership_id, invitee_email, initial_role_id,
                 status, token_digest, expires_at, created_by_platform_profile_id)
            VALUES
                (@invitation_id, @company_id, @membership_id, @invitee_email, @role_id,
                 'Pending', @token_digest, @expires_at, @actor_profile_id);
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction))
        {
            invitationCommand.Parameters.AddWithValue("invitation_id", invitationId);
            invitationCommand.Parameters.AddWithValue("company_id", companyId);
            invitationCommand.Parameters.AddWithValue("membership_id", membershipId);
            invitationCommand.Parameters.AddWithValue("invitee_email", inviteeEmail);
            invitationCommand.Parameters.AddWithValue("role_id", roleId.Value);
            invitationCommand.Parameters.AddWithValue("token_digest", tokenDigest);
            invitationCommand.Parameters.AddWithValue("expires_at", expiresAt);
            invitationCommand.Parameters.AddWithValue("actor_profile_id", actorProfileId);
            await invitationCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecordAuditAsync(
            tenantTransaction,
            actorProfileId,
            "platform.company.initial_administrator.invited",
            "Invitation",
            invitationId,
            new { status = "Pending", role = "Company Administrator" },
            cancellationToken);

        await tenantTransaction.CommitAsync(cancellationToken);
        return new InitialAdministratorInvitation(
            invitationId,
            companyId,
            inviteeEmail,
            expiresAt);
    }

    public async Task RevokeUndeliveredInvitationAsync(
        Guid companyId,
        Guid invitationId,
        Guid actorUserId,
        Guid actorProfileId,
        CancellationToken cancellationToken)
    {
        await using var tenantTransaction = await BeginPlatformTransactionAsync(
            actorUserId,
            cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE security.invitations
            SET status = 'Revoked', updated_at = now()
            WHERE id = @invitation_id
              AND company_id = @company_id
              AND status = 'Pending'
            RETURNING membership_id;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction);
        command.Parameters.AddWithValue("invitation_id", invitationId);
        command.Parameters.AddWithValue("company_id", companyId);
        var membershipId = await command.ExecuteScalarAsync(cancellationToken) as Guid?;
        if (membershipId is null)
        {
            throw new InvalidOperationException(
                "The undelivered invitation could not be revoked.");
        }

        await using (var membershipCommand = new NpgsqlCommand(
            """
            UPDATE security.memberships
            SET status = 'Revoked', updated_at = now()
            WHERE id = @membership_id
              AND company_id = @company_id
              AND status = 'Pending'
              AND user_profile_id IS NULL;
            """,
            tenantTransaction.Connection,
            tenantTransaction.Transaction))
        {
            membershipCommand.Parameters.AddWithValue("membership_id", (Guid)membershipId);
            membershipCommand.Parameters.AddWithValue("company_id", companyId);
            if (await membershipCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(
                    "The pending membership for an undelivered invitation could not be revoked.");
            }
        }

        await RecordAuditAsync(
            tenantTransaction,
            actorProfileId,
            "platform.company.initial_administrator_invitation.revoked",
            "Invitation",
            invitationId,
            new { status = "Revoked", reason = "delivery_failed" },
            cancellationToken);
        await tenantTransaction.CommitAsync(cancellationToken);
    }

    private ValueTask<TenantTransaction> BeginPlatformTransactionAsync(
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        transactionFactory.BeginAsync(
            new TenantTransactionContext(actorUserId, null, IsPlatformOwner: true),
            cancellationToken);

    private async Task RecordAuditAsync(
        TenantTransaction tenantTransaction,
        Guid actorProfileId,
        string operation,
        string targetType,
        Guid targetId,
        object summary,
        CancellationToken cancellationToken)
    {
        using var changeSummary = JsonDocument.Parse(JsonSerializer.Serialize(summary, JsonOptions));
        await auditWriter.RecordAsync(
            new AuditEntry(
                null,
                actorProfileId,
                operation,
                targetType,
                targetId,
                changeSummary,
                DateTimeOffset.UtcNow),
            tenantTransaction.Connection,
            tenantTransaction.Transaction,
            cancellationToken);
    }

    private static async Task<Guid?> FindInitialAdministratorRoleIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM security.company_roles
            WHERE company_id = @company_id
              AND name = 'Company Administrator'
              AND is_active;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("company_id", companyId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid roleId ? roleId : null;
    }

    private static async Task<bool> HasInitialAdministratorInvitationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid companyId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM security.invitations
                WHERE company_id = @company_id
                  AND initial_role_id = @role_id
                  AND status IN ('Pending', 'Accepted')
            );
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("company_id", companyId);
        command.Parameters.AddWithValue("role_id", roleId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<Company?> FindCompanyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid companyId,
        CancellationToken cancellationToken,
        bool forUpdate = false)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT id, legal_name, trade_name, country_code, legal_identifier, address,
                   contact_details, status, subscription_plan_reference, subscription_status,
                   subscription_started_at, subscription_ends_at, created_at, updated_at
            FROM platform.companies
            WHERE id = @company_id
            {(forUpdate ? "FOR UPDATE" : string.Empty)};
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("company_id", companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCompany(reader) : null;
    }

    private static Company ReadCompany(NpgsqlDataReader reader)
    {
        var contactDetails = JsonSerializer.Deserialize<CompanyContactDetails>(
            reader.GetString(6),
            JsonOptions);
        var status = Enum.TryParse<CompanyStatus>(reader.GetString(7), out var parsedStatus)
            ? parsedStatus
            : throw new InvalidOperationException("The database returned an unsupported company status.");

        return Company.Restore(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            contactDetails ?? new CompanyContactDetails(null, null),
            status,
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetFieldValue<DateTimeOffset>(13));
    }

    private static void AddCompanyParameters(NpgsqlCommand command, Company company)
    {
        command.Parameters.AddWithValue("id", company.Id);
        command.Parameters.AddWithValue("legal_name", company.LegalName);
        command.Parameters.AddWithValue("trade_name", DbValue(company.TradeName));
        command.Parameters.AddWithValue("country_code", company.CountryCode);
        command.Parameters.AddWithValue("legal_identifier", DbValue(company.LegalIdentifier));
        command.Parameters.AddWithValue("address", DbValue(company.Address));
        command.Parameters.Add(new NpgsqlParameter<string>(
            "contact_details",
            JsonSerializer.Serialize(company.ContactDetails, JsonOptions))
        {
            NpgsqlDbType = NpgsqlDbType.Jsonb
        });
        command.Parameters.AddWithValue(
            "subscription_plan_reference",
            DbValue(company.SubscriptionPlanReference));
        command.Parameters.AddWithValue("subscription_status", DbValue(company.SubscriptionStatus));
        command.Parameters.AddWithValue(
            "subscription_started_at",
            DbValue(company.SubscriptionStartedAt));
        command.Parameters.AddWithValue("subscription_ends_at", DbValue(company.SubscriptionEndsAt));
    }

    private static object DbValue<T>(T? value) =>
        value is null ? DBNull.Value : value;
}
