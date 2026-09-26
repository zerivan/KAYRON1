using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class RelevanciaSemantica
{
    public double Calcular(string consulta, KAYRON.Core.MemoriaAprendida memoria)
    {
        var consultaTokens = Tokens(consulta);
        var memoriaTokens = Tokens($"{memoria.Chave} {memoria.Valor} {string.Join(' ', memoria.Tags)}");
        if (consultaTokens.Count == 0 || memoriaTokens.Count == 0)
            return 0;

        var intersecao = consultaTokens.Intersect(memoriaTokens).Count();
        var uniao = consultaTokens.Union(memoriaTokens).Count();
        var lexical = uniao == 0 ? 0 : (double)intersecao / uniao;
        var prefixos = consultaTokens
            .SelectMany(t => memoriaTokens.Where(m => m.StartsWith(t[..Math.Min(4, t.Length)], StringComparison.Ordinal)))
            .Distinct()
            .Count();
        var cobertura = (double)prefixos / Math.Max(consultaTokens.Count, 1);

        return Math.Clamp((lexical * 0.75) + (cobertura * 0.25), 0, 1);
    }

    private static HashSet<string> Tokens(string texto)
    {
        return Normalizar(texto)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalizar(string valor) =>
        valor.Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a")
            .Replace("é", "e").Replace("ê", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ô", "o").Replace("õ", "o")
            .Replace("ú", "u").Replace("ç", "c");
}
