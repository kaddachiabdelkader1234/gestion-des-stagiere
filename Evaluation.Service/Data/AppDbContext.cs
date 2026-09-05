using Microsoft.EntityFrameworkCore;
using Evaluation.Service.Models;
using EvaluationEntity = Evaluation.Service.Models.Evaluation;
using AuditEntryEntity = Evaluation.Service.Models.AuditEntry;

namespace Evaluation.Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<EvaluationEntity> Evaluations => Set<EvaluationEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();

    /// <summary>
    /// Read-only projection from <c>CandidatureAccepted</c>. Never written by a request handler.
    /// </summary>
    public DbSet<StagiaireAffectation> StagiaireAffectations => Set<StagiaireAffectation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EvaluationEntity>(entity =>
        {
            entity.ToTable("Evaluations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TypeEvaluation).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Statut).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Commentaire).HasColumnType("text");
            entity.Property(x => x.Note).HasPrecision(3, 1);
            entity.Property(x => x.StagiaireNom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.StagiairePrenom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.StagiaireEmail).HasMaxLength(200);

            // Every scoped read filters on one of these three.
            entity.HasIndex(x => x.StagiaireId);
            entity.HasIndex(x => x.EncadrantId);
            entity.HasIndex(x => x.UtilisateurId);
            // The admin list filters by Statut (EnAttente, Soumise, Validee).
            entity.HasIndex(x => x.Statut);

            // One evaluation of a given type per stagiaire, enforced by the database and not only by
            // the pre-insert check: two concurrent submits would both pass that check and both insert.
            entity.HasIndex(x => new { x.StagiaireId, x.TypeEvaluation }).IsUnique();
        });

        modelBuilder.Entity<StagiaireAffectation>(entity =>
        {
            entity.ToTable("StagiaireAffectations");
            entity.HasKey(x => x.StagiaireId);

            entity.Property(x => x.Nom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Prenom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Email).HasMaxLength(200);

            entity.HasIndex(x => x.EncadrantId);
            entity.HasIndex(x => x.UtilisateurId);
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
