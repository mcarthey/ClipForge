using ClipForge.Models;
using Microsoft.EntityFrameworkCore;

namespace ClipForge.Data;

public class ClipForgeDbContext : DbContext
{
    public ClipForgeDbContext(DbContextOptions<ClipForgeDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.Filename).HasMaxLength(255).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ThumbnailPath).HasMaxLength(500);
            entity.Property(e => e.Duration).HasColumnType("decimal(10,2)");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Assets)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Platform);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(50);
            entity.Property(e => e.TimelineDefinition).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Templates)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TimelineDefinition).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Platform).HasMaxLength(50);
            entity.Property(e => e.OutputPath).HasMaxLength(500);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.ProcessingJobs)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ProcessingJobs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
