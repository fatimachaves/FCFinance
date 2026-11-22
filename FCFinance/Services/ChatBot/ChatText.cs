using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FCFinance.Services.ChatBot
{
    // Ajudantes de texto do bot: ler valores em reais e comparar palavras sem acento/maiúscula.
    public static partial class ChatText
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static string Money(decimal value) => value.ToString("C", PtBr);

        // Lê valores como: 45 | 45,9 | 45,90 | 1.234,56 | 1234.56 | R$ 50 | r$1.500
        // Retorna null se o texto não for um valor.
        public static decimal? ParseMoney(string text)
        {
            var s = text.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
            if (!MoneyPattern().IsMatch(s)) return null;

            int lastDot = s.LastIndexOf('.'), lastComma = s.LastIndexOf(',');
            if (lastComma >= 0 && lastDot >= 0)
            {
                // Os dois aparecem: o último é o separador decimal.
                s = lastComma > lastDot ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
            }
            else if (lastComma >= 0)
            {
                s = s.Replace(',', '.'); // 45,90
            }
            else if (lastDot >= 0 && s.Length - lastDot - 1 == 3)
            {
                s = s.Replace(".", ""); // 1.500 = mil e quinhentos (padrão brasileiro)
            }
            if (s.Count(c => c == '.') > 1) return null;

            if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return null;
            if (decimal.Round(value, 2) != value) return null; // mais de 2 casas decimais
            return value;
        }

        // "Não", "nao", "NÃO!" -> "nao"
        public static string Normalize(string text)
        {
            var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || c == ' '))
                    sb.Append(c);
            return sb.ToString().Trim();
        }

        [GeneratedRegex(@"^\d[\d.,]*$")]
        private static partial Regex MoneyPattern();
    }
}
