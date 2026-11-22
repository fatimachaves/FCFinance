namespace FCFinance.Models
{
    // Tamanhos máximos dos textos. Usados no banco (AppDbContext) e na validação dos DTOs.
    // Se mudar aqui, ajuste também o maxlength dos campos no site e crie uma migration.
    public static class Limites
    {
        public const int NomeCategoria = 60;
        public const int NomeLancamento = 120;
        public const int NomeMeta = 100;
    }
}
