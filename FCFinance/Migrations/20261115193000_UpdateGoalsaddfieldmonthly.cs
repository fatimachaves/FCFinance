using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGoalsaddfieldmonthly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMonthly",
                table: "Goals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReferenceDate",
                table: "Goals",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMonthly",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "ReferenceDate",
                table: "Goals");
        }
    }
}
