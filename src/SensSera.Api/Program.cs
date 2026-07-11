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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using SensSera.Application.Options;
using SensSera.Api.BackgroundJobs;
using SensSera.Api.Hubs;
using SensSera.Api.Realtime;


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

 // Authentication: JWT bearer (web clients) + device token (ingestion only)

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {   
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Key)), 
            RoleClaimType = "role",
            NameClaimType = "sub",
        };

        // WebSockets can't send an Authorization header, so SignalR clients pass
        // the JWT as ?access_token=. Only honour it on the hub path.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(
        DeviceTokenAuthenticationHandler.SchemeName, _=> {});

// Default policy: require JWT auth on all endpoints unless overriden
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

        

// Rate limiters: 100 req/min for ingest, 10 req/min for auth (brute force protection)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ingest", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("auth", o=>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
    });
});

// 1 MB payload limit applied globally
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1024 * 1024));
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter(
            System.Text.Json.JsonNamingPolicy.CamelCase));
});

builder.Services.AddValidatorsFromAssemblyContaining<GreenhouseRequestValidator>();

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IGreenhouseService, GreenhouseService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IThresholdService, ThresholdService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReadingService, ReadingService>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddHostedService<ThresholdEvaluationJob>();
builder.Services.AddHostedService<RollupJob>();
builder.Services.AddHostedService<DeviceHeartbeatJob>();

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

// SignalR uses its own JSON protocol, separate from AddControllers().AddJsonOptions().
// Mirror the same camelCase + string-enum settings so hub payloads match the REST contract.
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(
                System.Text.Json.JsonNamingPolicy.CamelCase));
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Dev-only docs; opt out of the default-deny fallback policy so the browser can reach them without a JWT.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// RFC 7807 Problem Details for all unhandled exceptions
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("LocalDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");

await DataSeeder.SeedAsync(app.Services);

app.Run();
