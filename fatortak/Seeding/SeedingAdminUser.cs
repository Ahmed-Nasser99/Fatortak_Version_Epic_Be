using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Seeding
{
    public static class SeedingAdminUser
    {
        public static async Task Seed(IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                await context.Database.MigrateAsync();
                await userManager.SeedAdminAsync();
            }
        }
        private static async Task SeedAdminAsync(this UserManager<ApplicationUser> userManager)
        {
            var adminEmail = "admin@fatortak.net";
            var adminUser = new ApplicationUser
            {
                UserName = "FatortakAdmin",
                FirstName = "Fatortak",
                LastName = "Admin",
                Role = RoleEnum.SysAdmin.ToString(),
                Email = adminEmail,
                IsActive = true,
                EmailConfirmed = true,
            };

            var user = await userManager.FindByEmailAsync(adminUser.Email);
            if (user == null)
            {
                // Create user and check result
                var createResult = await userManager.CreateAsync(adminUser, "Ahmed123@5aild");
                if (createResult.Succeeded)
                {
                    Console.WriteLine("Admin user created successfully.");
                }
                else
                {
                    // Log errors if user creation fails
                    foreach (var error in createResult.Errors)
                    {
                        Console.WriteLine($"User creation error: {error.Description}");
                    }
                }
            }
        }
    }
}
