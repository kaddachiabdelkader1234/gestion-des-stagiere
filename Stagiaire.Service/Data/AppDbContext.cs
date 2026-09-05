using Microsoft.EntityFrameworkCore;
using Stagiaire.Service.Models;
using StagiaireEntity = Stagiaire.Service.Models.Stagiaire;

namespace Stagiaire.Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StagiaireEntity> Stagiaires => Set<StagiaireEntity>();

    public DbSet<JournalEntry> JournalEntrees => Set<JournalEntry>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StagiaireEntity>(entity =>
        {
            entity.ToTable("Stagiaires");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Nom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Prenom).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Departement).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Ecole).IsRequired().HasMaxLength(200);

            // Enums are stored as text, not ordinals: a readable column that survives reordering
            // of the enum members.
            entity.Property(x => x.Statut).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.TypeStage).HasConversion<string>().HasMaxLength(20);

            entity.Property(x => x.Motivation).HasMaxLength(2000);
            entity.Property(x => x.CvCheminFichier).HasMaxLength(500);
            entity.Property(x => x.CvNomFichier).HasMaxLength(255);
            entity.Property(x => x.EncadrantNom).HasMaxLength(200);
            entity.Property(x => x.MotifRejet).HasMaxLength(1000);

            // The admin queue filters on Statut and orders by DateSoumission; a learner looks up
            // their own rows by UtilisateurId and an encadrant by EncadrantId.
            entity.HasIndex(x => x.Statut);
            entity.HasIndex(x => x.UtilisateurId);
            entity.HasIndex(x => x.EncadrantId);
            entity.HasIndex(x => x.DateSoumission);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
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

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("JournalEntrees");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Texte).IsRequired().HasMaxLength(4000);
            entity.Property(x => x.CommentaireEncadrant).HasMaxLength(2000);

            // One entry per stagiaire per week, enforced by the database rather than only by a
            // pre-insert check: two concurrent submits would both pass the check and both insert.
            // This also covers the only read pattern there is — one stagiaire's entries, newest
            // first — so no second index is needed.
            entity.HasIndex(x => new { x.StagiaireId, x.DateEntree }).IsUnique();

            entity.HasOne(x => x.Stagiaire)
                .WithMany(x => x.JournalEntrees)
                .HasForeignKey(x => x.StagiaireId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
