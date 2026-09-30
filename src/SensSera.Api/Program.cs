using Scalar.AspNetCore;
using Serilog;
using Microsoft.EntityFrameworkCore;
using SensSera.Infrastructure.Persistence;
using SensSera.Api.Middleware;
using SensSera.Application.Interfaces;
using SensSera.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
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
using Anthropic;
using System.Threading.RateLimiting;


// Bootstrap logger — replaced by full Serilog config after host builds
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
        // AspNetCore request logs include the full query string — and SignalR sends the
        // JWT as ?access_token=. Drop them to Warning so tokens never reach the logs.
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
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
    // The middleware default is 503, which clients read as "server down" rather than "slow down".
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("ingest", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("auth", o=>
    {
        o.PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        o.Window = TimeSpan.FromMinutes(1);
    });
    // Insight "Ask" calls the LLM (slow + costs money) — cap per tenant, not per IP,
    // partitioned on the trusted org_id claim so one org can't drain another's budget.
    options.AddPolicy("insights", httpContext =>
    {
        var orgId = httpContext.User.FindFirst("org_id")?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(orgId, _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) });
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
// Registration alone only puts validators in DI — this makes MVC actually run them on model
// binding (400 on failure). Without it every FluentValidation rule is dead code.
builder.Services.AddFluentValidationAutoValidation();

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

// LLM insight layer (§7.10). Non-secret config bound + validated on start (mirrors JwtOptions).
builder.Services.AddOptions<LlmOptions>()
    .Bind(builder.Configuration.GetSection(LlmOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The API key is read once here at the composition root and never bound into a logged options
// object. Provider is a config switch: "Groq" (free, OpenAI-compatible — dev/testing) or the
// default Anthropic path.
var llmProvider = builder.Configuration["Llm:Provider"];
var llmApiKey = builder.Configuration["Llm:ApiKey"];

if (string.Equals(llmProvider, "Groq", StringComparison.OrdinalIgnoreCase))
{
    // Typed HttpClient carries Groq's OpenAI-compatible base address + bearer key.
    builder.Services.AddHttpClient<ILlmClient, GroqLlmClient>(client =>
    {
        client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        if (!string.IsNullOrWhiteSpace(llmApiKey))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", llmApiKey);
    });
}
else
{
    // AnthropicClient holds the key (user-secrets in dev, Key Vault in prod). When absent, the
    // SDK falls back to the ANTHROPIC_API_KEY environment variable.
    builder.Services.AddSingleton(_ =>
        string.IsNullOrWhiteSpace(llmApiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = llmApiKey });
    builder.Services.AddScoped<ILlmClient, AnthropicLlmClient>();
}

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IInsightService, InsightService>();
builder.Services.AddHostedService<ThresholdEvaluationJob>();
builder.Services.AddHostedService<RollupJob>();
builder.Services.AddHostedService<DeviceHeartbeatJob>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Browser origins allowed to call the API with credentials (refresh-token cookie flow).
// Defaults to the Next.js dev server; prod sets Cors:AllowedOrigins to the exact frontend URL(s).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    is { Length: > 0 } origins ? origins : ["http://localhost:3000"];

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
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
else
{
    app.UseHsts();
}

// RFC 7807 Problem Details for all unhandled exceptions
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");

// Containers opt in to applying migrations on start; locally `dotnet ef database update` stays explicit.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// The demo org/admin has a well-known password — never create it outside dev/demo setups.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:DemoData"))
    await DataSeeder.SeedAsync(app.Services);

app.Run();

// Exposes the entry point to WebApplicationFactory in integration tests.
public partial class Program;
