using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Value",
                table: "Goals");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentValue",
                table: "Goals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TargetValue",
                table: "Goals",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentValue",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "TargetValue",
                table: "Goals");

            migrationBuilder.AddColumn<float>(
                name: "Value",
                table: "Goals",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }
    }
}
