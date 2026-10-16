using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RutaProducto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInitiativeReadyToBuildAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadyToBuildAt",
                table: "ini_initiatives",
                type: "datetime(6)",
                nullable: true);

            // Initiatives already ready have no recorded moment: their last modification is the best available.
            migrationBuilder.Sql(
                "UPDATE `ini_initiatives` SET `ReadyToBuildAt` = `UpdatedAt` WHERE `Status` = 'ReadyToBuild';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadyToBuildAt",
                table: "ini_initiatives");
        }
    }
}
