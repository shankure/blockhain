using Microsoft.EntityFrameworkCore;
using SecureTrace.API.Models;

namespace SecureTrace.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Evidence> Evidences => Set<Evidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(128).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(16).IsRequired();
        });

        // Case
        modelBuilder.Entity<Case>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.CaseNumber).IsUnique();
            entity.Property(c => c.CaseNumber).HasMaxLength(32).IsRequired();
            entity.Property(c => c.Title).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Status).HasMaxLength(16).IsRequired();

            entity.HasOne(c => c.CreatedBy)
                  .WithMany(u => u.Cases)
                  .HasForeignKey(c => c.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Evidence
        modelBuilder.Entity<Evidence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
            entity.Property(e => e.EvidenceType).HasMaxLength(32).IsRequired();

            entity.HasOne(e => e.Case)
                  .WithMany(c => c.Evidences)
                  .HasForeignKey(e => e.CaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            // No cascade on uploaded-by to avoid multi-cascade path conflicts
            entity.HasOne(e => e.UploadedBy)
                  .WithMany()
                  .HasForeignKey(e => e.UploadedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
