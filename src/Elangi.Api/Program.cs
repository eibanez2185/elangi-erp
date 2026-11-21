using Elangi.Api.Authentication;
using Elangi.Api.Authorization;
using Elangi.Api.Configuration;
using Elangi.Api.Errors;
using Elangi.Modules.Audit.Application;
using Elangi.Modules.Audit.Infrastructure;
using Elangi.Api.Endpoints.Platform;
using Elangi.Modules.Platform.Application;
using Elangi.Modules.Platform.Infrastructure;
using Elangi.Modules.Security.Application;
using Elangi.Modules.Security.Infrastructure;
using Elangi.Shared.Infrastructure.Postgres;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = SupabaseJwtSettings.FromConfiguration(builder.Configuration);
var databaseConnectionString = builder.Configuration.GetConnectionString("ApplicationDatabase");
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:ApplicationDatabase must be configured for the elangi_runtime database role.");
}

if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins must include the trusted frontend origins.");
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(NpgsqlDataSource.Create(databaseConnectionString));
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PATCH", "PUT", "DELETE")
            .WithHeaders("Accept", "Authorization", "Content-Type"));
});
builder.Services.AddScoped<CurrentActor>();
builder.Services.AddScoped<ICurrentActor>(services => services.GetRequiredService<CurrentActor>());
builder.Services.AddScoped<TenantTransactionFactory>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<IAuditRepository, PostgresAuditRepository>();
builder.Services.AddScoped<ICompanyAccessReader, PostgresCompanyAccessReader>();
builder.Services.AddScoped<CompanyAuthorizationService>();
builder.Services.AddScoped<IPlatformAccessReader, PostgresPlatformAccessReader>();
builder.Services.AddScoped<PlatformAuthorizationService>();
builder.Services.AddScoped<IPlatformCompanyRepository, PostgresPlatformCompanyRepository>();
builder.Services.AddScoped<CompanyAdministrationService>();
builder.Services.AddScoped<InitialAdministratorInvitationService>();
builder.Services.AddSingleton(
    InvitationEmailSettingsFactory.Create(builder.Configuration));
builder.Services.AddScoped<IInvitationEmailSender, SmtpInvitationEmailSender>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => SupabaseJwtBearerOptions.Configure(options, jwtSettings));
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlatformOwner", policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new PlatformOwnerRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, CompanyPermissionPolicy>();
builder.Services.AddScoped<IAuthorizationHandler, PlatformOwnerPolicy>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();
app.MapPlatformCompanyEndpoints();
app.MapInitialAdministratorInvitationEndpoints();

app.Run();

public partial class Program;
