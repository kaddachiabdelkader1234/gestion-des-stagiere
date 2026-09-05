using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evaluation.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationOwnershipScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-corrected: the generator emitted AlterColumn<long>(uuid -> bigint), which fails at
            // runtime — PostgreSQL cannot cast uuid to bigint, with or without a USING clause
            // ("column \"EncadrantId\" cannot be cast automatically to type bigint").
            //
            // Drop and re-add instead. That discards the column's contents, which is sound here and
            // only here: the values were Guids that could never correspond to an auth-service user id,
            // so there is nothing meaningful to preserve, and `Evaluations` was verified empty
            // (select count(*) = 0) before this was written.
            //
            // Deliberately no defaultValue: adding a NOT NULL column without one fails loudly if the
            // table turns out to have rows in some other environment. For a column that decides who
            // may read an evaluation, a failed migration is a far better outcome than every existing
            // row silently becoming owned by encadrant 0.
            migrationBuilder.DropColumn(
                name: "EncadrantId",
                table: "Evaluations");

            migrationBuilder.AddColumn<long>(
                name: "EncadrantId",
                table: "Evaluations",
                type: "bigint",
                nullable: false);

            // Hand-corrected: the generator added defaultValue: "" to both. The model declares no
            // default, so keeping them would leave DEFAULT constraints the snapshot does not describe
            // — the same correction made in PROJECT_WORK_LOG.md §17. The table is empty, so there is
            // nothing to backfill.
            migrationBuilder.AddColumn<string>(
                name: "StagiaireNom",
                table: "Evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "StagiairePrenom",
                table: "Evaluations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AddColumn<long>(
                name: "UtilisateurId",
                table: "Evaluations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StagiaireAffectations",
                columns: table => new
                {
                    StagiaireId = table.Column<Guid>(type: "uuid", nullable: false),
                    UtilisateurId = table.Column<long>(type: "bigint", nullable: true),
                    EncadrantId = table.Column<long>(type: "bigint", nullable: true),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagiaireAffectations", x => x.StagiaireId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_EncadrantId",
                table: "Evaluations",
                column: "EncadrantId");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_StagiaireId",
                table: "Evaluations",
                column: "StagiaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_StagiaireId_TypeEvaluation",
                table: "Evaluations",
                columns: new[] { "StagiaireId", "TypeEvaluation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_UtilisateurId",
                table: "Evaluations",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_StagiaireAffectations_EncadrantId",
                table: "StagiaireAffectations",
                column: "EncadrantId");

            migrationBuilder.CreateIndex(
                name: "IX_StagiaireAffectations_UtilisateurId",
                table: "StagiaireAffectations",
                column: "UtilisateurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StagiaireAffectations");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_EncadrantId",
                table: "Evaluations");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_StagiaireId",
                table: "Evaluations");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_StagiaireId_TypeEvaluation",
                table: "Evaluations");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_UtilisateurId",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "StagiaireNom",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "StagiairePrenom",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "UtilisateurId",
                table: "Evaluations");

            // Mirrors the Up(): drop and re-add rather than cast back, since bigint -> uuid is no more
            // castable than the reverse.
            migrationBuilder.DropColumn(
                name: "EncadrantId",
                table: "Evaluations");

            migrationBuilder.AddColumn<Guid>(
                name: "EncadrantId",
                table: "Evaluations",
                type: "uuid",
                nullable: false);
        }
    }
}
