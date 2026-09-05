using Microsoft.EntityFrameworkCore;
using ConventionEntity = Convention.Service.Models.Convention;
using AuditEntryEntity = Convention.Service.Models.AuditEntry;

namespace Convention.Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ConventionEntity> Conventions => Set<ConventionEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConventionEntity>(entity =>
        {
            entity.ToTable("Conventions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StatutSignature).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CheminPdf).IsRequired().HasMaxLength(500);
            entity.Property(x => x.StagiaireId).IsRequired();
            entity.Property(x => x.StagiaireNom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.StagiairePrenom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.StagiaireEmail).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Departement).IsRequired().HasMaxLength(150);
            entity.Property(x => x.DateDebut).IsRequired();
            entity.Property(x => x.DateFin).IsRequired();

            // Every scoped read filters on one of these, so both are indexed. Deliberately nullable:
            // a row with no owner is visible to an admin only.
            entity.HasIndex(x => x.UtilisateurId);
            entity.HasIndex(x => x.EncadrantId);
            // The stagiaire's own dashboard and the idempotence check in CandidatureAcceptedConsumer
            // both look a convention up by this.
            entity.HasIndex(x => x.StagiaireId);
            // The admin list filters by StatutSignature (EnAttente, Signee, Refusee).
            entity.HasIndex(x => x.StatutSignature);
        });

        modelBuilder.Entity<AuditEntryEntity>(entity =>
        {
            entity.ToTable("AuditEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).IsRequired().HasMaxLength(100);
            entity.Property(x => x.UserRole).HasMaxLength(20);
            entity.Property(x => x.UserEmail).HasMaxLength(200);
            entity.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.Details).HasMaxLength(500);
            entity.Property(x => x.TraceId).HasMaxLength(100);
            entity.HasIndex(x => x.Action);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.EntityType);
            entity.HasIndex(x => x.Timestamp);
        });
    }
}