using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCFinance.Migrations
{
    /// <inheritdoc />
    public partial class SeedValueTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cria os tipos fixos (1 = Receita, 2 = Despesa) só se ainda não existirem.
            // Em bancos antigos eles já foram cadastrados à mão; em banco novo são criados aqui.
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [ValueTypes] ON;
                IF NOT EXISTS (SELECT 1 FROM [ValueTypes] WHERE [Id] = 1)
                    INSERT INTO [ValueTypes] ([Id], [Name]) VALUES (1, N'Receita');
                IF NOT EXISTS (SELECT 1 FROM [ValueTypes] WHERE [Id] = 2)
                    INSERT INTO [ValueTypes] ([Id], [Name]) VALUES (2, N'Despesa');
                SET IDENTITY_INSERT [ValueTypes] OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não apaga os tipos: categorias e lançamentos dependem deles.
        }
    }
}
