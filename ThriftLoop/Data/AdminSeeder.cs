// Data/AdminSeeder.cs - Make it non-static
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;

namespace ThriftLoop.Data;

public class AdminSeeder  // Removed 'static' keyword
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminSeeder>>();

        // Check if admin already exists
        var adminExists = await context.Users.AnyAsync(u => u.Email == "admin@thriftloop.com");

        if (!adminExists)
        {
            var admin = new User
            {
                Email = "admin@thriftloop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin"),
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow,
                IsDisabled = false
            };

            context.Users.Add(admin);
            await context.SaveChangesAsync();

            logger.LogInformation("Admin user created: admin@thriftloop.com / Admin");
        }
    }
}