using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stagiaire.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidatureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.AddColumn<string>(
                name: "CvCheminFichier",
                table: "Stagiaires",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvNomFichier",
                table: "Stagiaires",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateDecision",
                table: "Stagiaires",
                type: "timestamp with time zone",
                nullable: true);

            // Hand-adjusted default: the generator emitted 0001-01-01, which would stamp every
            // pre-existing row with a nonsense submission date. CURRENT_TIMESTAMP gives them a
            // plausible one. The application always sets this explicitly on insert.
            migrationBuilder.AddColumn<DateTime>(
                name: "DateSoumission",
                table: "Stagiaires",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Ecole",
                table: "Stagiaires",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "EncadrantId",
                table: "Stagiaires",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncadrantNom",
                table: "Stagiaires",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotifRejet",
                table: "Stagiaires",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Motivation",
                table: "Stagiaires",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            // Hand-adjusted default: the generator emitted "" for this enum-as-string column, which
            // is not a valid TypeStage and would throw when EF materialized a pre-existing row.
            // "PFE" is the enum's own default.
            migrationBuilder.AddColumn<string>(
                name: "TypeStage",
                table: "Stagiaires",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PFE");

            migrationBuilder.AddColumn<long>(
                name: "UtilisateurId",
                table: "Stagiaires",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stagiaires_DateSoumission",
                table: "Stagiaires",
                column: "DateSoumission");

            migrationBuilder.CreateIndex(
                name: "IX_Stagiaires_EncadrantId",
                table: "Stagiaires",
                column: "EncadrantId");

            migrationBuilder.CreateIndex(
                name: "IX_Stagiaires_Statut",
                table: "Stagiaires",
                column: "Statut");

            migrationBuilder.CreateIndex(
                name: "IX_Stagiaires_UtilisateurId",
                table: "Stagiaires",
                column: "UtilisateurId");

            // Hand-added: StatutStagiaire.Accepte was renamed to Acceptee to match the brief's
            // ACCEPTEE. Statut is persisted as text, so any row written under the old name would
            // fail to materialize. No-op when no such rows exist.
            migrationBuilder.Sql(
                @"UPDATE ""Stagiaires"" SET ""Statut"" = 'Acceptee' WHERE ""Statut"" = 'Accepte';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE ""Stagiaires"" SET ""Statut"" = 'Accepte' WHERE ""Statut"" = 'Acceptee';");

            migrationBuilder.DropIndex(
                name: "IX_Stagiaires_DateSoumission",
                table: "Stagiaires");

            migrationBuilder.DropIndex(
                name: "IX_Stagiaires_EncadrantId",
                table: "Stagiaires");

            migrationBuilder.DropIndex(
                name: "IX_Stagiaires_Statut",
                table: "Stagiaires");

            migrationBuilder.DropIndex(
                name: "IX_Stagiaires_UtilisateurId",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "CvCheminFichier",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "CvNomFichier",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "DateDecision",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "DateSoumission",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "Ecole",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "EncadrantId",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "EncadrantNom",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "MotifRejet",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "Motivation",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "TypeStage",
                table: "Stagiaires");

            migrationBuilder.DropColumn(
                name: "UtilisateurId",
                table: "Stagiaires");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");
        }
    }
}
