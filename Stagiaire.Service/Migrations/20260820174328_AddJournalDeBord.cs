using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stagiaire.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalDeBord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntrees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StagiaireId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateEntree = table.Column<DateOnly>(type: "date", nullable: false),
                    Texte = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CommentaireEncadrant = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CommentaireParId = table.Column<long>(type: "bigint", nullable: true),
                    DateCommentaire = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateModification = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntrees_Stagiaires_StagiaireId",
                        column: x => x.StagiaireId,
                        principalTable: "Stagiaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntrees_StagiaireId_DateEntree",
                table: "JournalEntrees",
                columns: new[] { "StagiaireId", "DateEntree" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntrees");
        }
    }
}
