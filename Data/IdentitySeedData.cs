using Microsoft.AspNetCore.Identity;

namespace schedule.Data
{
    public static class IdentitySeedData
    {
        public const string AdminEmail = "admin@example.com";
        public const string AdminPassword = "Admin@123";

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            foreach (var role in new[] { "Admin", "User" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var admin = await userManager.FindByEmailAsync(AdminEmail);
            if (admin == null)
            {
                admin = new IdentityUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(admin, AdminPassword);
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}
