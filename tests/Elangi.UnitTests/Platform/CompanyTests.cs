using Elangi.Modules.Platform.Domain;

namespace Elangi.UnitTests.Platform;

public sealed class CompanyTests
{
    [Fact]
    public void Create_RequiresLegalNameAndCountryCode()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Company.Create(" ", "Trade", "US", null, null, null));
        Assert.ThrowsAny<ArgumentException>(
            () => Company.Create("Legal", "Trade", "USA", null, null, null));
        Assert.ThrowsAny<ArgumentException>(
            () => Company.Create("Legal", "Trade", "ZZ", null, null, null));
    }

    [Fact]
    public void Create_ReportsCountryCodeErrorAgainstItsField()
    {
        var exception = Assert.Throws<CompanyValidationException>(
            () => Company.Create("Legal", "Trade", "57", null, null, null));

        Assert.Equal("countryCode", exception.ParamName);
        Assert.Contains("prefijo telefónico", exception.Message, StringComparison.Ordinal);
        Assert.Contains("countryCode", exception.Errors.Keys);
    }

    [Fact]
    public void Create_NormalizesIdentityAndStartsSuspended()
    {
        var company = Company.Create(
            "  Example Legal Name  ",
            "  Example Trade Name ",
            "ec",
            " 12345 ",
            " Quito ",
            new CompanyContactDetails("office@example.test", "+593123456789"));

        Assert.Equal("Example Legal Name", company.LegalName);
        Assert.Equal("Example Trade Name", company.TradeName);
        Assert.Equal("EC", company.CountryCode);
        Assert.Equal("12345", company.LegalIdentifier);
        Assert.Equal("Quito", company.Address);
        Assert.Equal(CompanyStatus.Suspended, company.Status);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.example.test")]
    public void Create_RejectsInvalidContactEmail(string email)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Company.Create(
                "Example",
                null,
                "US",
                null,
                null,
                new CompanyContactDetails(email, null)));
    }

    [Fact]
    public void WithIdentity_RejectsBlankLegalNameAndUpdatesOtherFields()
    {
        var company = CreateCompany();

        Assert.ThrowsAny<ArgumentException>(
            () => company.WithIdentity(" ", null, "US", null, null, null));

        var updated = company.WithIdentity(
            "Updated Legal Name",
            null,
            "US",
            null,
            null,
            null);

        Assert.Equal("Updated Legal Name", updated.LegalName);
        Assert.Null(updated.TradeName);
    }

    [Fact]
    public void WithSubscription_RejectsEndBeforeStart()
    {
        var company = CreateCompany();
        var startsAt = DateTimeOffset.UtcNow;

        Assert.ThrowsAny<ArgumentException>(
            () => company.WithSubscription("standard", "trial", startsAt, startsAt.AddDays(-1)));
    }

    [Fact]
    public void WithStatus_AllowsSupportedStates()
    {
        var company = CreateCompany();
        Assert.Equal(CompanyStatus.Active, company.WithStatus(CompanyStatus.Active).Status);
        Assert.Equal(CompanyStatus.Suspended, company.WithStatus(CompanyStatus.Suspended).Status);
    }

    private static Company CreateCompany() =>
        Company.Create("Example Legal Name", "Example", "US", null, null, null);
}
