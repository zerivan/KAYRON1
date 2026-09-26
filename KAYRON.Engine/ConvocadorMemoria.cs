using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ConvocadorMemoria
{
    private readonly IMemoria _memoria;
    private readonly IMemoriaAprendida _memoriaAprendida;
    private readonly MemoriaSemantica _memoriaSemantica;
    private readonly MemoriaPostgres _memoriaPostgres;

    public ConvocadorMemoria(
        IMemoria memoria,
        IMemoriaAprendida memoriaAprendida,
        MemoriaSemantica memoriaSemantica,
        MemoriaPostgres memoriaPostgres
    )
    {
        ArgumentNullException.ThrowIfNull(memoria);
        ArgumentNullException.ThrowIfNull(memoriaAprendida);
        ArgumentNullException.ThrowIfNull(memoriaSemantica);
        ArgumentNullException.ThrowIfNull(memoriaPostgres);

        _memoria = memoria;
        _memoriaAprendida = memoriaAprendida;
        _memoriaSemantica = memoriaSemantica;
        _memoriaPostgres = memoriaPostgres;
    }

    public void Convocar(
        string instrucao,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var texto = instrucao.Trim();

        contexto.Adicionar(
            "memoria:instrucao",
            texto);

        var ultimaInstrucao =
            _memoria.Recuperar("ultima_instrucao");

        if (!string.IsNullOrWhiteSpace(ultimaInstrucao))
        {
            contexto.Adicionar(
                "memoria:ultima_instrucao",
                ultimaInstrucao);
        }

        var ultimaIntencao =
            _memoria.Recuperar("ultima_intencao");

        if (!string.IsNullOrWhiteSpace(ultimaIntencao))
        {
            contexto.Adicionar(
                "memoria:ultima_intencao",
                ultimaIntencao);
        }
        var exata = _memoriaAprendida.Recuperar(texto);
        var memóriasRelevantes = exata is not null
            ? new[] { exata }
            : _memoriaPostgres.Pesquisar(texto, 3)
                .Where(EhConhecimentoUtilizavel)
                .ToArray();

        if (memóriasRelevantes.Count == 0)
        {
            memóriasRelevantes = _memoriaSemantica
                .Pesquisar(texto, _memoriaAprendida.Listar(), 3)
                .Where(EhConhecimentoUtilizavel)
                .ToArray();
        }

        foreach (var memoriaRelevante in memóriasRelevantes)
        {
            if (string.IsNullOrWhiteSpace(memoriaRelevante.Valor) ||
                memoriaRelevante.Valor.TrimStart().StartsWith(
                    "Pesquisa realizada para:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            contexto.Adicionar(
                $"memoria:aprendida:{memoriaRelevante.Chave}",
                memoriaRelevante.Valor);
        }

        var memóriasValidas = memóriasRelevantes
            .Where(m => !string.IsNullOrWhiteSpace(m.Valor) &&
                        !m.Valor.TrimStart().StartsWith(
                            "Pesquisa realizada para:",
                            StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (memóriasValidas.Length > 0)
        {
            contexto.Adicionar(
                "memoria:aprendida_relevante",
                string.Join(",", memóriasValidas.Select(m => m.Chave)));
        }
    }

    public KAYRON.Core.MemoriaAprendida? EncontrarRelevante(
        string instrucao)
    {
        var texto = Normalizar(instrucao);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var palavras =
            texto
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length >= 3)
                .ToArray();

        if (palavras.Length == 0)
        {
            return null;
        }

        KAYRON.Core.MemoriaAprendida? melhor = null;
        var melhorPontuacao = 0;

        foreach (var memoria in _memoriaAprendida.Listar().Where(EhConhecimentoUtilizavel))
        {
            if (string.IsNullOrWhiteSpace(memoria.Chave) ||
                string.IsNullOrWhiteSpace(memoria.Valor))
            {
                continue;
            }

            var chave = Normalizar(memoria.Chave);
            var valor = Normalizar(memoria.Valor);
            var pontuacao = 0;

            if (texto.Contains(
                    chave,
                    StringComparison.OrdinalIgnoreCase))
            {
                pontuacao += 100;
            }

            foreach (var palavra in palavras)
            {
                if (chave.Contains(
                        palavra,
                        StringComparison.OrdinalIgnoreCase))
                {
                    pontuacao += 20;
                }

                if (valor.Contains(
                        palavra,
                        StringComparison.OrdinalIgnoreCase))
                {
                    pontuacao += 5;
                }
            }

            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = memoria;
            }
        }

        return melhor;
    }

    private static bool EhConhecimentoUtilizavel(KAYRON.Core.MemoriaAprendida memoria)
    {
        if (!string.Equals(memoria.Tipo, "conhecimento", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(memoria.Origem, "pesquisa_verificada", StringComparison.OrdinalIgnoreCase) ||
            !memoria.Tags.Any(tag => string.Equals(tag, "verificado", StringComparison.OrdinalIgnoreCase)))
            return false;

        var referencia = memoria.AtualizadoEm > memoria.AprendidoEm
            ? memoria.AtualizadoEm
            : memoria.AprendidoEm;

        return DateTime.UtcNow - referencia <= TimeSpan.FromDays(7);
    }

    private static string Normalizar(string valor)
    {
        return valor
            .Trim()
            .ToLowerInvariant()
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("õ", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
    }
}
