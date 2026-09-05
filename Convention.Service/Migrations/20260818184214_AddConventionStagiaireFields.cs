using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convention.Service.Migrations
{
    /// <summary>
    /// Adds the stagiaire details the convention PDF is rendered from. They are copied onto the
    /// convention row by <c>CandidatureAcceptedConsumer</c> when the candidature is accepted, so
    /// generating a PDF never has to call back into Stagiaire.Service.
    /// </summary>
    /// <remarks>
    /// The generator emitted <c>defaultValue: new DateOnly(1, 1, 1)</c> and <c>defaultValue: ""</c>
    /// for these columns; both were removed by hand. `Conventions` is empty, so there is nothing to
    /// backfill, and the model declares no default — leaving one here would put a DEFAULT constraint
    /// in the database that the model snapshot does not describe, and would silently turn a future
    /// insert that forgets a column into a row dated 0001-01-01 instead of an error.
    /// </remarks>
    public partial class AddConventionStagiaireFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StagiaireNom",
                table: "Conventions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "StagiairePrenom",
                table: "Conventions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "StagiaireEmail",
                table: "Conventions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Departement",
                table: "Conventions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateDebut",
                table: "Conventions",
                type: "date",
                nullable: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateFin",
                table: "Conventions",
                type: "date",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateDebut",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "DateFin",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "Departement",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "StagiaireEmail",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "StagiaireNom",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "StagiairePrenom",
                table: "Conventions");
        }
    }
}
