using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PowerHub.Identity.Audit;
using PowerHub.Identity.Data;
using PowerHub.Identity.Tokens;

namespace PowerHub.Identity;

/// <summary>
/// Operational commands run as a release step or Kubernetes Job, never by serving replicas
/// (INF-MIG-002) and never through a public endpoint (NFR-SEC-013).
/// </summary>
public static class Cli
{
    public const string AdminPasswordVariable = "POWERHUB_ADMIN_PASSWORD";

    public static async Task<int> MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDb>().Database.MigrateAsync();
        await RoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>());
        return 0;
    }

    public static async Task<int> CreateAdminAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Cli));

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            var password = Environment.GetEnvironmentVariable(AdminPasswordVariable);
            if (string.IsNullOrEmpty(password))
            {
                logger.LogError("Set {Variable} to create a new administrator account", AdminPasswordVariable);
                return 1;
            }

            user = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                DisplayName = "Administrator",
                EmailConfirmed = true,
                CreatedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(),
            };

            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Administrator was not created: {Errors}", string.Join("; ", created.Errors.Select(error => error.Description)));
                return 1;
            }

            await users.AddToRoleAsync(user, Roles.User);
        }

        if (!await users.IsInRoleAsync(user, Roles.Administrator))
        {
            await users.AddToRoleAsync(user, Roles.Administrator);
            await scope.ServiceProvider.GetRequiredService<AuditLog>()
                .WriteAsync(AuditActions.RoleGranted, user.Id, true, detail: Roles.Administrator);
        }

        logger.LogInformation("Administrator role is assigned to user {UserId}", user.Id);
        return 0;
    }
}
