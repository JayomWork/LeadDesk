using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Data;

public static class SeedData
{
    public static readonly string[] Roles = ["Admin", "ProjectLead", "Manager", "Developer", "SrDeveloper", "QA", "ClientViewer"];

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateUserAsync(userManager, "admin@leaddesk.local", "Admin User", "Admin");
        await CreateUserAsync(userManager, "lead@leaddesk.local", "Project Lead", "ProjectLead");
        await CreateUserAsync(userManager, "dev@leaddesk.local", "Developer User", "Developer");
        await CreateUserAsync(userManager, "qa@leaddesk.local", "QA User", "QA");
        await CreateUserAsync(userManager, "manager@leaddesk.local", "Manager User", "Manager");
        await CreateUserAsync(userManager, "client@leaddesk.local", "Client Viewer", "ClientViewer");

        if (!await context.TeamMembers.AnyAsync())
        {
            context.TeamMembers.AddRange(
                new TeamMember { FullName = "Jayom", Role = "Project Lead", Email = "lead@leaddesk.local" },
                new TeamMember { FullName = "Abhishek", Role = "Developer", Email = "dev@leaddesk.local" },
                new TeamMember { FullName = "Shubham", Role = "QA", Email = "qa@leaddesk.local" }
            );
        }

        if (!await context.AccountPractices.AnyAsync())
        {
            context.AccountPractices.AddRange(
                new AccountPractice { Name = "Brook Practice", ClientContactName = "Client Contact", Status = "Active", Notes = "Sample behavioral health practice." },
                new AccountPractice { Name = "TTT Practice", ClientContactName = "Client Contact", Status = "Active", Notes = "Sample account for progress tracking." }
            );
        }

        await context.SaveChangesAsync();

        if (!await context.WorkItems.AnyAsync())
        {
            var account = await context.AccountPractices.FirstAsync();
            var dev = await context.TeamMembers.FirstOrDefaultAsync(x => x.Role == "Developer");
            var qa = await context.TeamMembers.FirstOrDefaultAsync(x => x.Role == "QA");
            context.WorkItems.AddRange(
                new WorkItem { AccountPracticeId = account.Id, Title = "Telehealth transcription summary issue", ModuleName = "Telehealth", Type = "Bug", Priority = "High", Status = "In Dev", AssignedDeveloperId = dev?.Id, AssignedQaId = qa?.Id, DueDate = DateTime.UtcNow.AddDays(3), LatestUpdate = "Initial sample item created." },
                new WorkItem { AccountPracticeId = account.Id, Title = "Add account progress summary view", ModuleName = "Reports", Type = "Enhancement", Priority = "Medium", Status = "Analysis", AssignedDeveloperId = dev?.Id, DueDate = DateTime.UtcNow.AddDays(7), LatestUpdate = "Need UI and filter confirmation." }
            );
            await context.SaveChangesAsync();
        }
    }

    private static async Task CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user != null) return;

        user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName };
        var result = await userManager.CreateAsync(user, "Admin@12345");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
