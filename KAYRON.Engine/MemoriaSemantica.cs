using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class MemoriaSemantica
{
    private readonly IEmbeddingMemoria _embedding;

    public MemoriaSemantica(IEmbeddingMemoria embedding)
    {
        _embedding = embedding;
    }

    public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Pesquisar(
        string consulta,
        IEnumerable<KAYRON.Core.MemoriaAprendida> memorias,
        int limite = 5)
    {
        return PesquisarAsync(consulta, memorias, limite).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyCollection<KAYRON.Core.MemoriaAprendida>> PesquisarAsync(
        string consulta,
        IEnumerable<KAYRON.Core.MemoriaAprendida> memorias,
        int limite = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consulta) || limite <= 0)
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();

        var vetorConsulta = await _embedding.GerarAsync(consulta, cancellationToken);
        var candidatos = new List<(KAYRON.Core.MemoriaAprendida Memoria, double Score)>();

        foreach (var memoria in memorias)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var texto = $"{memoria.Chave} {memoria.Valor} {string.Join(' ', memoria.Tags)}";
            var vetor = memoria.Embedding;
            if (vetor is null || vetor.Length == 0)
                vetor = await _embedding.GerarAsync(texto, cancellationToken);

            var score = Cosine(vetorConsulta, vetor);
            candidatos.Add((memoria, score));
        }

        var semanticos = candidatos
            .Where(x => x.Score >= 0.55)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memoria.Importancia)
            .Take(Math.Min(limite, 10))
            .Select(x => x.Memoria)
            .ToArray();

        if (semanticos.Length > 0)
            return semanticos;

        return candidatos
            .Select(x => (x.Memoria, Score: PontuarLexical(consulta, x.Memoria)))
            .Where(x => x.Score >= 0.30)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memoria.Importancia)
            .Take(Math.Min(limite, 10))
            .Select(x => x.Memoria)
            .ToArray();
    }

    private static double PontuarLexical(
        string consulta,
        KAYRON.Core.MemoriaAprendida memoria)
    {
        var consultaTokens = Tokenizar(consulta);
        if (consultaTokens.Count == 0)
            return 0;

        var memoriaTokens = Tokenizar(
            $"{memoria.Chave} {memoria.Valor} {string.Join(' ', memoria.Tags)}");
        var comuns = consultaTokens.Intersect(memoriaTokens).Count();

        return comuns == 0
            ? 0
            : (double)comuns / consultaTokens.Count;
    }

    private static HashSet<string> Tokenizar(string texto)
    {
        var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "o", "as", "os", "um", "uma", "uns", "umas", "de", "da", "do",
            "das", "dos", "e", "ou", "que", "é", "em", "no", "na", "nos", "nas",
            "para", "por", "com", "como", "qual", "quais", "usada", "usado", "ser",
            "são", "sao", "mais", "se", "ao", "aos", "à", "às"
        };

        var tokens = System.Text.RegularExpressions.Regex.Split(
                texto.ToLowerInvariant(), @"[^\p{L}\p{N}.]+")
            .Where(token => token.Length >= 2 && !stopwords.Contains(token));

        return tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double produto = 0;
        double normaA = 0;
        double normaB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            produto += a[i] * b[i];
            normaA += a[i] * a[i];
            normaB += b[i] * b[i];
        }

        return normaA == 0 || normaB == 0
            ? 0
            : produto / (Math.Sqrt(normaA) * Math.Sqrt(normaB));
    }
}
