using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stagiaire.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentCheminFichier",
                table: "Stagiaires",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNomFichier",
                table: "Stagiaires",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DocumentCheminFichier", table: "Stagiaires");
            migrationBuilder.DropColumn(name: "DocumentNomFichier", table: "Stagiaires");
        }
    }
}
