using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created and migrated
        await context.Database.MigrateAsync();

        // Seed Roles
        string[] roleNames = { "Admin", "Employee", "Customer" };
        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Seed Admin User
        var adminEmail = "admin@hutech.edu.vn";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            var createAdmin = await userManager.CreateAsync(adminUser, "Admin@123");
            if (createAdmin.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Seed Employee User
        var employeeEmail = "nv@hutech.edu.vn";
        var employeeUser = await userManager.FindByEmailAsync(employeeEmail);
        if (employeeUser == null)
        {
            employeeUser = new IdentityUser
            {
                UserName = employeeEmail,
                Email = employeeEmail,
                EmailConfirmed = true
            };
            var createEmployee = await userManager.CreateAsync(employeeUser, "Staff@123");
            if (createEmployee.Succeeded)
            {
                await userManager.AddToRoleAsync(employeeUser, "Employee");
            }
        }

        // Seed Customer User
        var customerEmail = "kh@hutech.edu.vn";
        var customerUser = await userManager.FindByEmailAsync(customerEmail);
        if (customerUser == null)
        {
            customerUser = new IdentityUser
            {
                UserName = customerEmail,
                Email = customerEmail,
                EmailConfirmed = true
            };
            var createCustomer = await userManager.CreateAsync(customerUser, "Customer@123");
            if (createCustomer.Succeeded)
            {
                await userManager.AddToRoleAsync(customerUser, "Customer");
            }
        }

        // DB is clean. Starting with an empty catalog.
    }
}
