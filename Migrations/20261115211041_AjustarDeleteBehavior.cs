using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class AjustarDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_CategoriesType_CategoryTypeId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredValues_Categories_CategoryId",
                table: "RegisteredValues");

            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredValues_ValueTypes_ValueTypeId",
                table: "RegisteredValues");

            migrationBuilder.DropTable(
                name: "CategoriesType");

            migrationBuilder.RenameColumn(
                name: "CategoryTypeId",
                table: "Categories",
                newName: "TypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_CategoryTypeId",
                table: "Categories",
                newName: "IX_Categories_TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_ValueTypes_TypeId",
                table: "Categories",
                column: "TypeId",
                principalTable: "ValueTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredValues_Categories_CategoryId",
                table: "RegisteredValues",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredValues_ValueTypes_ValueTypeId",
                table: "RegisteredValues",
                column: "ValueTypeId",
                principalTable: "ValueTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_ValueTypes_TypeId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredValues_Categories_CategoryId",
                table: "RegisteredValues");

            migrationBuilder.DropForeignKey(
                name: "FK_RegisteredValues_ValueTypes_ValueTypeId",
                table: "RegisteredValues");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "Categories",
                newName: "CategoryTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_TypeId",
                table: "Categories",
                newName: "IX_Categories_CategoryTypeId");

            migrationBuilder.CreateTable(
                name: "CategoriesType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriesType", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_CategoriesType_CategoryTypeId",
                table: "Categories",
                column: "CategoryTypeId",
                principalTable: "CategoriesType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredValues_Categories_CategoryId",
                table: "RegisteredValues",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegisteredValues_ValueTypes_ValueTypeId",
                table: "RegisteredValues",
                column: "ValueTypeId",
                principalTable: "ValueTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
