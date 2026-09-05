using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convention.Service.Migrations
{
    /// <summary>
    /// Adds the two ownership columns that make convention reads role-scoped, plus the indexes every
    /// scoped query filters on.
    /// </summary>
    /// <remarks>
    /// Both are nullable and intentionally left unbackfilled: Convention.Service cannot resolve the
    /// owner of a pre-existing row without calling into Stagiaire.Service, and a null simply means
    /// "admins only", which is the safe failure direction. Any convention created from
    /// <c>CandidatureAccepted</c> after this point carries both values.
    ///
    /// Nothing was hand-corrected here — unlike AddConventionStagiaireFields, the generator emitted
    /// no bogus defaults, because these columns are nullable.
    /// </remarks>
    public partial class AddConventionOwnershipScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EncadrantId",
                table: "Conventions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UtilisateurId",
                table: "Conventions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conventions_EncadrantId",
                table: "Conventions",
                column: "EncadrantId");

            migrationBuilder.CreateIndex(
                name: "IX_Conventions_StagiaireId",
                table: "Conventions",
                column: "StagiaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Conventions_UtilisateurId",
                table: "Conventions",
                column: "UtilisateurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conventions_EncadrantId",
                table: "Conventions");

            migrationBuilder.DropIndex(
                name: "IX_Conventions_StagiaireId",
                table: "Conventions");

            migrationBuilder.DropIndex(
                name: "IX_Conventions_UtilisateurId",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "EncadrantId",
                table: "Conventions");

            migrationBuilder.DropColumn(
                name: "UtilisateurId",
                table: "Conventions");
        }
    }
}
