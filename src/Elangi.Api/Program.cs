using Elangi.Api.Authentication;
using Elangi.Api.Authorization;
using Elangi.Api.Errors;
using Elangi.Modules.Audit.Application;
using Elangi.Modules.Audit.Infrastructure;
using Elangi.Modules.Security.Application;
using Elangi.Modules.Security.Infrastructure;
using Elangi.Shared.Infrastructure.Postgres;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = SupabaseJwtSettings.FromConfiguration(builder.Configuration);
var databaseConnectionString = builder.Configuration.GetConnectionString("ApplicationDatabase");

if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:ApplicationDatabase must be configured for the elangi_runtime database role.");
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(NpgsqlDataSource.Create(databaseConnectionString));
builder.Services.AddScoped<CurrentActor>();
builder.Services.AddScoped<ICurrentActor>(services => services.GetRequiredService<CurrentActor>());
builder.Services.AddScoped<TenantTransactionFactory>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<IAuditRepository, PostgresAuditRepository>();
builder.Services.AddScoped<ICompanyAccessReader, PostgresCompanyAccessReader>();
builder.Services.AddScoped<CompanyAuthorizationService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => SupabaseJwtBearerOptions.Configure(options, jwtSettings));
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthorizationHandler, CompanyPermissionPolicy>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.Run();

public partial class Program;
