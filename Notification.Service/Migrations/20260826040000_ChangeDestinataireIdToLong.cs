using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Service.Migrations
{
    /// <inheritdoc />
    [Migration("20260826040000_ChangeDestinataireIdToLong")]
    [DbContext(typeof(Data.AppDbContext))]
    public partial class ChangeDestinataireIdToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-corrected: PostgreSQL cannot cast uuid to bigint with AlterColumn.
            // Drop and re-add instead. The table's notification rows are either empty or
            // contain throwaway smoke-test data (consumers log-only; no real notifications
            // have been created through the API). Nothing meaningful is lost.
            //
            // No DropIndex needed: the InitialCreate migration did not create any secondary
            // indexes on this table, so there is nothing to drop.

            migrationBuilder.DropColumn(
                name: "DestinataireId",
                table: "Notifications");

            migrationBuilder.AddColumn<long>(
                name: "DestinataireId",
                table: "Notifications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DestinataireId",
                table: "Notifications",
                column: "DestinataireId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_DestinataireId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DestinataireId",
                table: "Notifications");

            migrationBuilder.AddColumn<Guid>(
                name: "DestinataireId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
