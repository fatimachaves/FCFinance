using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoTabelaGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredValues_valueTypes_ValueTypeId",
                table: "RegisteredValues");

            migrationBuilder.DropPrimaryKey(
                name: "PK_valueTypes",
                table: "valueTypes");

            migrationBuilder.RenameTable(
                name: "valueTypes",
                newName: "ValueTypes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ValueTypes",
                table: "ValueTypes",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedWhen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<float>(type: "real", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredValues_ValueTypes_ValueTypeId",
                table: "RegisteredValues",
                column: "ValueTypeId",
                principalTable: "ValueTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredValues_ValueTypes_ValueTypeId",
                table: "RegisteredValues");

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ValueTypes",
                table: "ValueTypes");

            migrationBuilder.RenameTable(
                name: "ValueTypes",
                newName: "valueTypes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_valueTypes",
                table: "valueTypes",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredValues_valueTypes_ValueTypeId",
                table: "RegisteredValues",
                column: "ValueTypeId",
                principalTable: "valueTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
