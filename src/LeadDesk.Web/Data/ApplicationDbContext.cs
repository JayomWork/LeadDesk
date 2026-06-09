using LeadDesk.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeadDesk.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AccountPractice> AccountPractices => Set<AccountPractice>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemAttachment> WorkItemAttachments => Set<WorkItemAttachment>();
    public DbSet<WorkItemUpdate> WorkItemUpdates => Set<WorkItemUpdate>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingWorkItem> MeetingWorkItems => Set<MeetingWorkItem>();
    public DbSet<ReleasePlan> ReleasePlans => Set<ReleasePlan>();
    public DbSet<ReleaseWorkItem> ReleaseWorkItems => Set<ReleaseWorkItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AccountPractice>().HasIndex(x => x.Name);
        builder.Entity<WorkItem>().HasIndex(x => new { x.Status, x.Priority });
        builder.Entity<WorkItem>().HasIndex(x => x.DueDate);
        builder.Entity<WorkItem>().HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<WorkItemAttachment>()
            .HasIndex(x => new { x.WorkItemId, x.CreatedAt });

        builder.Entity<WorkItemAttachment>()
            .HasOne(x => x.WorkItem)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<WorkItem>()
            .HasOne(x => x.AssignedDeveloper)
            .WithMany()
            .HasForeignKey(x => x.AssignedDeveloperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<WorkItem>()
            .HasOne(x => x.AssignedQa)
            .WithMany()
            .HasForeignKey(x => x.AssignedQaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<WorkItem>()
            .HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MeetingWorkItem>()
            .HasIndex(x => new { x.MeetingId, x.WorkItemId })
            .IsUnique();

        builder.Entity<ReleaseWorkItem>()
            .HasIndex(x => new { x.ReleasePlanId, x.WorkItemId })
            .IsUnique();
    }
}
