using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roomify.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationOptimisticConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Npgsql rozpoznaje systemowe xmin i pomija fizyczne ADD COLUMN; zmiana utrwala model/historię.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "reservations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Provider pomija także DROP systemowego atrybutu; dane tabeli pozostają bez zmian.
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "reservations");
        }
    }
}
