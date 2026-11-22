using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class TermosECriacaoDoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CriadoEm",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermosAceitosEm",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriadoEm",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TermosAceitosEm",
                table: "AspNetUsers");
        }
    }
}
