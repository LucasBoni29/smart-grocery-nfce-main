using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartGrocery.Api.Data;
using SmartGrocery.Api.Models;

namespace SmartGrocery.Api.Services;

// Liga um nome de produto bruto (como veio na nota) a um CanonicalProduct. Se achar um
// existente parecido o suficiente, reusa; senao cria um novo. Sem IA: so normalizacao de
// texto + sobreposicao de palavras (ProductNormalizer).
public class CanonicalProductMatcher
{
    // Fracao de palavras em comum (Jaccard) pra considerar "e o mesmo produto".
    // Calibrado no olho com os dados que temos ate agora - revisar quando aparecerem
    // mais notas e o dicionario de abreviacao crescer.
    private const double MatchThreshold = 0.6;

    private readonly AppDbContext _context;

    public CanonicalProductMatcher(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CanonicalProduct> MatchOrCreateAsync(string rawName, CancellationToken cancellationToken = default)
    {
        var normalized = ProductNormalizer.Normalize(rawName);
        var tokens = ProductNormalizer.Tokenize(normalized);

        // Volume baixo por enquanto (uso pessoal) - trazer tudo pra memoria e comparar
        // e mais simples e rapido o suficiente do que montar busca por similaridade no banco.
        var candidates = await _context.CanonicalProducts.AsNoTracking().ToListAsync(cancellationToken);

        CanonicalProduct? best = null;
        var bestScore = 0.0;

        foreach (var candidate in candidates)
        {
            var candidateTokens = ProductNormalizer.Tokenize(ProductNormalizer.Normalize(candidate.Name));
            var score = ProductNormalizer.JaccardSimilarity(tokens, candidateTokens);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best is not null && bestScore >= MatchThreshold)
        {
            return await _context.CanonicalProducts.FirstAsync(c => c.Id == best.Id, cancellationToken);
        }

        var created = new CanonicalProduct
        {
            Name = ToDisplayName(normalized, rawName),
            CreatedAt = DateTime.UtcNow,
        };
        _context.CanonicalProducts.Add(created);
        return created;
    }

    private static string ToDisplayName(string normalized, string fallback)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return fallback.Trim();

        var textInfo = CultureInfo.GetCultureInfo("pt-BR").TextInfo;
        return textInfo.ToTitleCase(normalized.ToLower(CultureInfo.GetCultureInfo("pt-BR")));
    }
}
