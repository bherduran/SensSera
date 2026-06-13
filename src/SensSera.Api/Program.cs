using Scalar.AspNetCore;
using Serilog;
using Microsoft.EntityFrameworkCore;
using SensSera.Infrastructure.Persistence;
using SensSera.Api.Middleware;
using SensSera.Application.Interfaces;
using SensSera.Infrastructure.Services;
using FluentValidation;
using SensSera.Api.Validators;
using SensSera.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;

// Bootstrap logger — replaced by full Serilog config after host builds
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

// Inject system clock — services use TimeProvider, never DateTime.UtcNow directly
builder.Services.AddSingleton(TimeProvider.System);

// Device token auth scheme — used by /api/ingest only (not JWT)
builder.Services.AddAuthentication(DeviceTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(
        DeviceTokenAuthenticationHandler.SchemeName, _ => { });

// 100 req/min fixed window on ingest routes
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ingest", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
    });
});

// 1 MB payload limit applied globally
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1024 * 1024));
});

builder.Services.AddValidatorsFromAssemblyContaining<GreenhouseRequestValidator>();

// Services
builder.Services.AddScoped<ITenantContext, StubTenantContext>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IGreenhouseService, GreenhouseService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Allow Next.js dev server — credentials needed for refresh-token cookie flow
builder.Services.AddCors(options =>
    options.AddPolicy("LocalDev", policy =>
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// RFC 7807 Problem Details for all unhandled exceptions
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("LocalDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

await DataSeeder.SeedAsync(app.Services);

app.Run();
