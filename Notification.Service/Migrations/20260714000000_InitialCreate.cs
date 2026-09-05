using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Notification.Service.Data;

#nullable disable

namespace Notification.Service.Migrations;

// See Stagiaire.Service's migration: without [DbContext]/[Migration] EF finds no migrations and
// never creates the database.
[DbContext(typeof(AppDbContext))]
[Migration("20260714000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DestinataireId = table.Column<Guid>(type: "uuid", nullable: false),
                DestinataireRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Message = table.Column<string>(type: "text", nullable: false),
                Lu = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");
    }
}