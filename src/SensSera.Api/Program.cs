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
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)), 
            RoleClaimType = "role",
            NameClaimType = "sub",    
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
