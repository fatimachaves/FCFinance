using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class DadosPorUsuarioEValorDecimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Goals_IsPriority",
                table: "Goals");

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "RegisteredValues",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "RegisteredValues",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Goals",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Categories",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            // Dados que já existiam (sem dono) passam para o usuário "fatinha".
            // Categorias existentes ficam com UserId = NULL: viram categorias do sistema.
            // Se o usuário não existir neste banco, nada é alterado.
            migrationBuilder.Sql(@"
                UPDATE r SET r.UserId = u.Id
                FROM RegisteredValues r CROSS JOIN AspNetUsers u
                WHERE r.UserId = '' AND u.UserName = 'fatinha';

                UPDATE g SET g.UserId = u.Id
                FROM Goals g CROSS JOIN AspNetUsers u
                WHERE g.UserId = '' AND u.UserName = 'fatinha';");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredValues_UserId",
                table: "RegisteredValues",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Goals_UserId_IsPriority",
                table: "Goals",
                columns: new[] { "UserId", "IsPriority" },
                unique: true,
                filter: "[IsPriority] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId",
                table: "Categories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RegisteredValues_UserId",
                table: "RegisteredValues");

            migrationBuilder.DropIndex(
                name: "IX_Goals_UserId_IsPriority",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "RegisteredValues");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            migrationBuilder.AlterColumn<float>(
                name: "Value",
                table: "RegisteredValues",
                type: "real",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_IsPriority",
                table: "Goals",
                column: "IsPriority",
                unique: true,
                filter: "[IsPriority] = 1");
        }
    }
}
