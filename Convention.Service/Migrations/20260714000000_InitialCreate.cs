using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Convention.Service.Data;

#nullable disable

namespace Convention.Service.Migrations;

// See Stagiaire.Service's migration: without [DbContext]/[Migration] EF finds no migrations and
// never creates the database.
[DbContext(typeof(AppDbContext))]
[Migration("20260714000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Conventions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StagiaireId = table.Column<Guid>(type: "uuid", nullable: false),
                DateGeneration = table.Column<DateOnly>(type: "date", nullable: false),
                StatutSignature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CheminPdf = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Conventions", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Conventions");
    }
}