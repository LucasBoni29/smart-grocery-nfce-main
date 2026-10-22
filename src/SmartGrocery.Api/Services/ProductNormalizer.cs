using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartGrocery.Api.Services;

// Normalizacao de nome de produto sem IA: so regra fixa. O objetivo e aproximar duas
// escritas do mesmo produto real (ex: "LEITE ITALAC INT" e "LEITE TP INT ITALAC") o
// suficiente pra comparar por sobreposicao de palavras. Nao tenta adivinhar marca ou
// categoria - isso fica pra quando tiver mais dado acumulado (Fase Z do roadmap).
public static class ProductNormalizer
{
    // Abreviacoes comuns em nota fiscal de mercado. Lista cresce conforme aparecerem
    // casos novos - nao precisa cobrir tudo de uma vez.
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INT"] = "INTEGRAL",
        ["TP"] = "TIPO",
        ["GRAN"] = "GRANEL",
        ["COND"] = "CONDENSADO",
        ["REFRIG"] = "REFRIGERANTE",
        ["DET"] = "DETERGENTE",
        ["LIQ"] = "LIQUIDO",
        ["ALC"] = "ALCOOL",
        ["TRAD"] = "TRADICIONAL",
        ["CONG"] = "CONGELADO",
        ["NAT"] = "NATURAL",
        ["CHOC"] = "CHOCOLATE",
        ["ACUC"] = "ACUCAR",
        ["BISC"] = "BISCOITO",
        ["MARG"] = "MARGARINA",
        ["REFR"] = "REFRESCO",
        ["SABAO"] = "SABAO",
    };

    // Palavras que nao ajudam a diferenciar produto nenhum - somem antes de comparar.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DE", "DO", "DA", "DOS", "DAS", "COM", "E", "A", "O", "EM", "PARA", "SEM",
    };

    private static readonly Regex TrailingCodeRegex = new(@"\s+\d{3,}$", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumericRegex = new(@"[^A-Z0-9\s]", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Nome "limpo" pra exibir: maiusculo, sem acento, sem o codigo interno que a loja
    // as vezes gruda no fim do nome (ex: "COXA DE FRANGO GRANEL KG 4056" -> "...KG").
    public static string Normalize(string rawName)
    {
        var text = RemoveDiacritics(rawName.Trim().ToUpperInvariant());
        text = NonAlphaNumericRegex.Replace(text, " ");
        text = TrailingCodeRegex.Replace(text, string.Empty);
        text = MultiSpaceRegex.Replace(text, " ").Trim();
        return text;
    }

    // Conjunto de palavras significativas, com abreviacao expandida, pra comparar
    // dois nomes por sobreposicao (Jaccard). Sem stemming, sem fuzzy match - so o basico.
    public static HashSet<string> Tokenize(string normalizedName)
    {
        var tokens = normalizedName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => Abbreviations.TryGetValue(token, out var expanded) ? expanded : token)
            .Where(token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return tokens;
    }

    public static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0;

        var intersection = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
