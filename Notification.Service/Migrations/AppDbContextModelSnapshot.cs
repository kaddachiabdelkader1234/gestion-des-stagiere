using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Notification.Service.Data;

#nullable disable

namespace Notification.Service.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.8")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("Notification.Service.Models.Notification", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<DateTime>("DateCreation")
                .HasColumnType("timestamp with time zone");

            b.Property<long>("DestinataireId")
                .HasColumnType("bigint");

            b.Property<string>("DestinataireRole")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<string>("Message")
                .IsRequired()
                .HasColumnType("text");

            b.Property<bool>("Lu")
                .ValueGeneratedOnAdd()
                .HasDefaultValue(false)
                .HasColumnType("boolean");

            b.Property<string>("Type")
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnType("character varying(40)");

            b.HasKey("Id");

            b.HasIndex("DestinataireId");

            b.ToTable("Notifications");
        });
    }
}