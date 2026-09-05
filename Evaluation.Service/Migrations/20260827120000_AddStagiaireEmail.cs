using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evaluation.Service.Migrations
{
    /// <summary>
    /// Adds StagiaireEmail to Evaluation and Email to StagiaireAffectation so the
    /// EvaluationSubmitted event can carry the stagiaire's email for notification delivery.
    /// Both tables are empty at migration time, so no data backfill is needed.
    /// </summary>
    public partial class AddStagiaireEmail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StagiaireEmail",
                table: "Evaluations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "StagiaireAffectations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StagiaireEmail",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "StagiaireAffectations");
        }
    }
}
