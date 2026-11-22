using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roomify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomOptimisticConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Npgsql rozpoznaje systemowe xmin: operacja aktualizuje model i historię,
            // wygenerowany SQL nie dodaje fizycznej kolumny do rooms.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "rooms",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Provider nie usuwa systemowej kolumny PostgreSQL przy cofnięciu mapowania.
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "rooms");
        }
    }
}
