using Microsoft.EntityFrameworkCore;                       
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensSera.Domain.Entities;
using SensSera.Domain.Enums;

namespace SensSera.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (await db.Organizations.AnyAsync())
            return;

        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        var org = new Organization
        {
            Name = "Demo Org",
            Slug = "demo",
            CreatedAt = now,
            UpdatedAt = now,
        };

        var admin = new User
        {
            Email = "admin@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!"),
            Role = Role.Admin,
            Organization = org,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Organizations.Add(org);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        logger.LogInformation("Seed data created: org={Slug}, admin={Email}", org.Slug, admin.Email);        
    }
}