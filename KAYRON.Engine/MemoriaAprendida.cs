using KAYRON.Core;

namespace KAYRON.Engine;

public class MemoriaAprendida : IMemoriaAprendida
{
    private readonly Dictionary<string, KAYRON.Core.MemoriaAprendida> _dados = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMemoriaAprendidaPersistente _memoriaPersistente;
    private readonly MemoriaPostgres _memoriaPostgres;

    public MemoriaAprendida(IMemoriaAprendidaPersistente memoriaPersistente, MemoriaPostgres memoriaPostgres)
    {
        ArgumentNullException.ThrowIfNull(memoriaPersistente);
        ArgumentNullException.ThrowIfNull(memoriaPostgres);
        _memoriaPersistente = memoriaPersistente;
        _memoriaPostgres = memoriaPostgres;
        _memoriaPostgres.Inicializar();
        foreach (var memoria in _memoriaPersistente.Listar())
        {
            if (EhMemoriaValida(memoria))
                _dados[memoria.Chave] = memoria;
        }
    }

    public void Aprender(string chave, string valor)
    {
        Aprender(chave, valor, "fato", 3, false, "agente", Array.Empty<string>());
    }

    public void Aprender(string chave, string valor, string tipo, int importancia, bool explicita, string origem, IEnumerable<string>? tags)
    {
        if (string.IsNullOrWhiteSpace(chave)) throw new ArgumentException("A chave da memória aprendida não pode estar vazia.", nameof(chave));
        if (string.IsNullOrWhiteSpace(valor)) throw new ArgumentException("O valor da memória aprendida não pode estar vazio.", nameof(valor));

        if (EhConteudoPesquisaTransitório(valor))
            throw new InvalidOperationException("Conteúdo transitório de pesquisa não pode ser persistido como memória.");

        importancia = Math.Clamp(importancia, 1, 5);


        var agora = DateTime.UtcNow;
        var existente = _dados.Values.FirstOrDefault(m => m.Chave.Equals(chave.Trim(), StringComparison.OrdinalIgnoreCase));
        var memoria = new KAYRON.Core.MemoriaAprendida
        {
            Chave = chave.Trim(), Valor = valor.Trim(), Tipo = string.IsNullOrWhiteSpace(tipo) ? "fato" : tipo.Trim(),
            Importancia = explicita ? 5 : importancia, Explicita = explicita, Origem = string.IsNullOrWhiteSpace(origem) ? "agente" : origem.Trim(),
            Tags = (tags ?? Array.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            AprendidoEm = existente?.AprendidoEm ?? agora, AtualizadoEm = agora, UltimaRecuperacaoEm = existente?.UltimaRecuperacaoEm
        };
        _dados[memoria.Chave] = memoria;
        _memoriaPersistente.Salvar(memoria);
        _memoriaPostgres.Sincronizar(memoria);
    }

    public bool Remover(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || !_dados.Remove(chave.Trim())) return false;
        return _memoriaPersistente.Remover(chave.Trim());
    }

    public KAYRON.Core.MemoriaAprendida? Recuperar(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || !_dados.TryGetValue(chave.Trim(), out var memoria)) return null;
        if (!EhMemoriaValida(memoria)) return null;
        MarcarRecuperacao(memoria);
        return memoria;
    }

    public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Pesquisar(string consulta, int limite = 5)
    {
        if (string.IsNullOrWhiteSpace(consulta) || limite <= 0)
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();

        var normalizada = Normalizar(consulta);
        var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "o", "as", "os", "um", "uma", "uns", "umas", "de", "da", "do",
            "das", "dos", "e", "ou", "que", "é", "em", "no", "na", "nos", "nas",
            "para", "por", "com", "como", "qual", "quais", "ao", "aos", "à", "às",
            "se", "mais", "sobre", "ser", "são", "sao"
        };

        var termos = normalizada
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 4 && !stopwords.Contains(t))
            .Distinct()
            .ToArray();

        if (termos.Length == 0)
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();

        var resultados = _dados.Values
            .Where(EhMemoriaValida)
            .Select(memoria => new
            {
                Memoria = memoria,
                Pontuacao = Pontuar(memoria, normalizada, termos),
                Relevancia = CalcularRelevancia(normalizada, memoria)
            })
            .Where(x => x.Pontuacao > 0 && x.Relevancia >= 0.25)
            .OrderByDescending(x => x.Pontuacao)
            .ThenByDescending(x => x.Relevancia)
            .ThenByDescending(x => x.Memoria.Importancia)
            .ThenByDescending(x => x.Memoria.UltimaRecuperacaoEm ?? DateTime.MinValue)
            .Take(Math.Min(limite, 10))
            .Select(x => x.Memoria)
            .ToArray();

        foreach (var memoria in resultados)
            MarcarRecuperacao(memoria);

        return resultados;
    }

    public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Listar() => _dados.Values.ToList().AsReadOnly();

    private void MarcarRecuperacao(KAYRON.Core.MemoriaAprendida memoria)
    {
        memoria.UltimaRecuperacaoEm = DateTime.UtcNow;
        _memoriaPersistente.Salvar(memoria);
        _memoriaPostgres.Sincronizar(memoria);
    }

    private static bool EhConteudoPesquisaTransitório(string valor)
    {
        var texto = valor.Trim();

        if (texto.StartsWith("Pesquisa realizada para:", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("Fontes recuperadas da pesquisa web para:", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("Informações encontradas para:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var marcadoresDeFalha = new[]
        {
            "provedor de IA ainda não foi configurado",
            "chave Gemini não foi configurada",
            "falha na API Gemini",
            "não retornou texto utilizável",
            "não é possível responder",
            "não contém informações técnicas",
            "não contendo informações técnicas",
            "incerteza total sobre o tema",
            "erro ao pesquisar",
            "[KAYRON_DECISAO]",
            "\"proxima_acao\"",
            "\"parametros\""
        };

        return marcadoresDeFalha.Any(marcador =>
            texto.Contains(marcador, StringComparison.OrdinalIgnoreCase));
    }

    private static double CalcularRelevancia(string consulta, KAYRON.Core.MemoriaAprendida memoria)
    {
        var consultaTokens = Tokens(consulta);
        var memoriaTokens = Tokens($"{memoria.Chave} {memoria.Valor} {string.Join(' ', memoria.Tags)}");
        if (consultaTokens.Count == 0 || memoriaTokens.Count == 0)
            return 0;

        var intersecao = consultaTokens.Intersect(memoriaTokens).Count();
        var uniao = consultaTokens.Union(memoriaTokens).Count();
        var lexical = uniao == 0 ? 0d : (double)intersecao / uniao;
        var cobertura = (double)consultaTokens.Count(token => memoriaTokens.Any(tokenMemoria =>
            tokenMemoria.StartsWith(token, StringComparison.Ordinal) ||
            token.StartsWith(tokenMemoria, StringComparison.Ordinal))) / consultaTokens.Count;

        return Math.Clamp((lexical * 0.70) + (cobertura * 0.30), 0, 1);
    }

    private static HashSet<string> Tokens(string texto)
    {
        return Normalizar(texto)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int Pontuar(KAYRON.Core.MemoriaAprendida memoria, string consulta, string[] termos)
    {
        var chave = Normalizar(memoria.Chave);
        var valor = Normalizar(memoria.Valor);
        var tags = memoria.Tags.Select(Normalizar).ToArray();
        var tipo = Normalizar(memoria.Tipo);
        var origem = Normalizar(memoria.Origem);
        var pontos = 0;

        if (string.Equals(chave, consulta, StringComparison.Ordinal)) pontos += 1000;
        else if (chave.Contains(consulta, StringComparison.Ordinal)) pontos += 300;

        foreach (var termo in termos)
        {
            if (chave.Contains(termo, StringComparison.Ordinal)) pontos += 80;
            if (tags.Any(tag => tag.Contains(termo, StringComparison.Ordinal))) pontos += 60;
            if (tipo.Contains(termo, StringComparison.Ordinal)) pontos += 25;
            if (origem.Contains(termo, StringComparison.Ordinal)) pontos += 10;
            if (valor.Contains(termo, StringComparison.Ordinal)) pontos += 5;
        }

        // Importância só pode desempatar uma memória que já tenha relação real com a consulta.
        if (pontos > 0)
        {
            pontos += memoria.Importancia * 3;
            if (memoria.Explicita) pontos += 10;
        }

        return pontos;
    }

    private static bool EhMemoriaValida(KAYRON.Core.MemoriaAprendida memoria)
    {
        if (!memoria.Tipo.Equals("conhecimento", StringComparison.OrdinalIgnoreCase))
            return true;

        var valor = memoria.Valor ?? string.Empty;
        var marcadoresInvalidos = new[]
        {
            "[KAYRON_DECISAO]",
            "\"proxima_acao\"",
            "\"parametros\"",
            "baseada em conhecimentos gerais",
            "com base em conhecimentos gerais",
            "já possuo essa informação",
            "posso realizar uma busca",
            "deseja que eu faça isso",
            "como posso ajudar você hoje",
            "fontes recuperadas da pesquisa web para:",
            "pesquisa realizada para:"
        };

        return !marcadoresInvalidos.Any(marcador =>
            valor.Contains(marcador, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalizar(string valor) =>
        valor.Trim().ToLowerInvariant()
            .Replace("á", "a").Replace("à", "a").Replace("ã", "a").Replace("â", "a")
            .Replace("é", "e").Replace("ê", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ô", "o").Replace("õ", "o")
            .Replace("ú", "u").Replace("ç", "c");
}

