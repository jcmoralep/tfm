using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RutaProducto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInitiativeLastActivityAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActivityAt",
                table: "ini_initiatives",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "ini_initiatives");
        }
    }
}
