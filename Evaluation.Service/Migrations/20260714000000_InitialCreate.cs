using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Evaluation.Service.Data;

#nullable disable

namespace Evaluation.Service.Migrations;

// See Stagiaire.Service's migration: without [DbContext]/[Migration] EF finds no migrations and
// never creates the database.
[DbContext(typeof(AppDbContext))]
[Migration("20260714000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Evaluations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StagiaireId = table.Column<Guid>(type: "uuid", nullable: false),
                EncadrantId = table.Column<Guid>(type: "uuid", nullable: false),
                TypeEvaluation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                DateEvaluation = table.Column<DateOnly>(type: "date", nullable: false),
                Note = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                Commentaire = table.Column<string>(type: "text", nullable: false),
                Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Evaluations", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Evaluations");
    }
}