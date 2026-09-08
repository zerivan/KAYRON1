using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ConvocadorMemoria
{
    private readonly IMemoria _memoria;
    private readonly IMemoriaAprendida _memoriaAprendida;

    public ConvocadorMemoria(
        IMemoria memoria,
        IMemoriaAprendida memoriaAprendida)
    {
        ArgumentNullException.ThrowIfNull(memoria);
        ArgumentNullException.ThrowIfNull(memoriaAprendida);

        _memoria = memoria;
        _memoriaAprendida = memoriaAprendida;
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

        var aprendidas =
            _memoriaAprendida.Listar();

        contexto.Adicionar(
            "memoria:aprendidas_total",
            aprendidas.Count.ToString());

        foreach (var memoria in aprendidas)
        {
            if (string.IsNullOrWhiteSpace(memoria.Chave) ||
                string.IsNullOrWhiteSpace(memoria.Valor))
            {
                continue;
            }

            contexto.Adicionar(
                $"memoria:aprendida:{memoria.Chave}",
                memoria.Valor);
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

        foreach (var memoria in _memoriaAprendida.Listar())
        {
            if (string.IsNullOrWhiteSpace(memoria.Chave) ||
                string.IsNullOrWhiteSpace(memoria.Valor))
            {
                continue;
            }

            var chave = Normalizar(memoria.Chave);
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
            }

            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = memoria;
            }
        }

        return melhor;
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
