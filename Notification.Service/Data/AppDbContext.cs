using Microsoft.EntityFrameworkCore;
using NotificationEntity = Notification.Service.Models.Notification;

namespace Notification.Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DestinataireRole).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Message).HasColumnType("text");
            entity.Property(x => x.Lu).HasDefaultValue(false);
            entity.Property(x => x.DateCreation).IsRequired();

            // Every scoped read filters on this — the equivalent of Evaluation.EncadrantId indexes.
            entity.HasIndex(x => x.DestinataireId);
        });
    }
}