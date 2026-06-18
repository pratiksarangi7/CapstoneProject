using CapstoneProjectAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CapstoneProjectAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Department> Departments => Set<Department>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
        public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Name)
                      .IsRequired()
                      .HasMaxLength(100);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);

                entity.Property(u => u.Name)
                      .IsRequired()
                      .HasMaxLength(150);

                entity.Property(u => u.Email)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.HasIndex(u => u.Email)
                      .IsUnique();

                entity.Property(u => u.PasswordHash)
                      .IsRequired();

                entity.HasOne(u => u.Department)
                      .WithMany(d => d.Users)
                      .HasForeignKey(u => u.DepartmentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(u => u.Manager)
                      .WithMany(u => u.Subordinates)
                      .HasForeignKey(u => u.ManagerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Title)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(d => d.Description)
                      .HasMaxLength(1000);

                entity.Property(d => d.DocumentStatus)
                      .IsRequired();

                entity.HasOne(d => d.CreatedByUser)
                      .WithMany(u => u.CreatedDocuments)
                      .HasForeignKey(d => d.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.CurrentApprover)
                      .WithMany()
                      .HasForeignKey(d => d.CurrentApproverUserId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(d => d.TargetDepartment)
                      .WithMany()
                      .HasForeignKey(d => d.TargetDepartmentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DocumentVersion>(entity =>
            {
                entity.HasKey(dv => dv.Id);

                entity.Property(dv => dv.OriginalFileName)
                      .IsRequired()
                      .HasMaxLength(300);

                entity.Property(dv => dv.StoredFileName)
                      .IsRequired()
                      .HasMaxLength(300);

                entity.Property(dv => dv.MimeType)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(dv => dv.ContentHash)
                      .HasMaxLength(64)
                      .IsRequired(false);

                entity.HasOne(dv => dv.Document)
                      .WithMany(d => d.Versions)
                      .HasForeignKey(dv => dv.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(dv => dv.UploadedByUser)
                      .WithMany(u => u.UploadedVersions)
                      .HasForeignKey(dv => dv.UploadedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ApprovalAction>(entity =>
            {
                entity.HasKey(aa => aa.Id);

                entity.Property(aa => aa.Action)
                      .IsRequired();

                entity.Property(aa => aa.Comments)
                      .HasMaxLength(500);

                entity.HasOne(aa => aa.Document)
                      .WithMany(d => d.ApprovalActions)
                      .HasForeignKey(aa => aa.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(aa => aa.DocumentVersion)
                      .WithMany(dv => dv.ApprovalActions)
                      .HasForeignKey(aa => aa.DocumentVersionId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(aa => aa.ApproverUser)
                      .WithMany(u => u.ApprovalActions)
                      .HasForeignKey(aa => aa.ApproverUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(aa => aa.ForwardedToDepartment)
                      .WithMany()
                      .HasForeignKey(aa => aa.ForwardedToDepartmentId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(al => al.Id);

                entity.Property(al => al.Action)
                      .IsRequired();

                entity.Property(al => al.Details)
                      .HasMaxLength(1000);

                entity.HasOne(al => al.PerformedByUser)
                      .WithMany(u => u.AuditLogs)
                      .HasForeignKey(al => al.PerformedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(al => al.Document)
                      .WithMany(d => d.AuditLogs)
                      .HasForeignKey(al => al.DocumentId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(al => al.DocumentVersion)
                      .WithMany(dv => dv.AuditLogs)
                      .HasForeignKey(al => al.DocumentVersionId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
