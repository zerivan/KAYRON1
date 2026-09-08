using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class InterpretadorLinguagem
    : IInterpretadorLinguagem
{
    private readonly ICatalogoFerramentas _catalogo;

    public InterpretadorLinguagem(
        ICatalogoFerramentas catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        _catalogo = catalogo;
    }

    public string Interpretar(
        string entrada,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return string.Empty;
        }

        var texto =
            entrada.Trim();

        var tokens =
            Tokenizar(texto);

        if (tokens.Length == 0)
        {
            return texto;
        }

        var vocabulario =
            CriarVocabulario();

        var alteracoes =
            new List<(string Original, string Corrigido)>();

        foreach (var token in tokens)
        {
            var palavra =
                LimparToken(token);

            if (palavra.Length < 4)
            {
                continue;
            }

            if (vocabulario.Contains(
                    palavra,
                    StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var melhor =
                EncontrarMelhorCorrespondencia(
                    palavra,
                    vocabulario);

            if (melhor is null)
            {
                continue;
            }

            if (melhor.Value.Distancia > LimiteDistancia(
                    palavra.Length))
            {
                continue;
            }

            if (melhor.Value.Distancia == 0)
            {
                continue;
            }

            alteracoes.Add(
                (palavra, melhor.Value.Palavra));
        }

        var resultado =
            texto;

        foreach (var alteracao in alteracoes)
        {
            resultado =
                SubstituirPalavra(
                    resultado,
                    alteracao.Original,
                    alteracao.Corrigido);
        }

        if (alteracoes.Count > 0)
        {
            contexto.Adicionar(
                "linguagem_corrigida",
                "sim");

            contexto.Adicionar(
                "correcoes_linguisticas",
                string.Join(
                    ", ",
                    alteracoes.Select(
                        alteracao =>
                            $"{alteracao.Original} -> {alteracao.Corrigido}")));
        }
        else
        {
            contexto.Adicionar(
                "linguagem_corrigida",
                "não");
        }

        contexto.Adicionar(
            "instrucao_interpretada",
            resultado);

        return resultado;
    }

    private List<string> CriarVocabulario()
    {
        var palavras =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var ferramenta in _catalogo.Listar())
        {
            AdicionarTexto(
                ferramenta.Nome,
                palavras);

            AdicionarTexto(
                ferramenta.Descricao,
                palavras);

            foreach (var operacao in
                     ferramenta.Capacidade.Operacoes)
            {
                AdicionarTexto(
                    operacao,
                    palavras);
            }

            foreach (var exemplo in
                     ferramenta.Capacidade.Exemplos)
            {
                AdicionarTexto(
                    exemplo,
                    palavras);
            }
        }

        return palavras.ToList();
    }

    private static void AdicionarTexto(
        string texto,
        HashSet<string> palavras)
    {
        foreach (var token in Tokenizar(texto))
        {
            var palavra =
                LimparToken(token);

            if (palavra.Length >= 4)
            {
                palavras.Add(
                    palavra);
            }
        }
    }

    private static string[] Tokenizar(
        string texto)
    {
        return texto.Split(
            [' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']'],
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static string LimparToken(
        string token)
    {
        return new string(
            token
                .Trim()
                .Where(
                    caractere =>
                        char.IsLetterOrDigit(caractere) ||
                        caractere == '-')
                .ToArray())
            .ToLowerInvariant();
    }

    private static (
        string Palavra,
        int Distancia)? EncontrarMelhorCorrespondencia(
        string palavra,
        IReadOnlyCollection<string> vocabulario)
    {
        string? melhorPalavra = null;
        var menorDistancia = int.MaxValue;

        foreach (var candidata in vocabulario)
        {
            if (Math.Abs(
                    candidata.Length -
                    palavra.Length) > 2)
            {
                continue;
            }

            var distancia =
                DistanciaLevenshtein(
                    palavra,
                    candidata);

            if (distancia < menorDistancia)
            {
                menorDistancia =
                    distancia;

                melhorPalavra =
                    candidata;
            }
        }

        if (melhorPalavra is null)
        {
            return null;
        }

        return (
            melhorPalavra,
            menorDistancia);
    }

    private static int LimiteDistancia(
        int tamanho)
    {
        return tamanho switch
        {
            <= 4 => 1,
            <= 7 => 2,
            _ => 3
        };
    }

    private static int DistanciaLevenshtein(
        string origem,
        string destino)
    {
        var anterior =
            new int[destino.Length + 1];

        var atual =
            new int[destino.Length + 1];

        for (var indice = 0;
             indice <= destino.Length;
             indice++)
        {
            anterior[indice] =
                indice;
        }

        for (var i = 1;
             i <= origem.Length;
             i++)
        {
            atual[0] =
                i;

            for (var j = 1;
                 j <= destino.Length;
                 j++)
            {
                var custo =
                    origem[i - 1] ==
                    destino[j - 1]
                        ? 0
                        : 1;

                atual[j] =
                    Math.Min(
                        Math.Min(
                            atual[j - 1] + 1,
                            anterior[j] + 1),
                        anterior[j - 1] + custo);
            }

            (anterior, atual) =
                (atual, anterior);
        }

        return anterior[destino.Length];
    }

    private static string SubstituirPalavra(
        string texto,
        string original,
        string corrigido)
    {
        var partes =
            TokenizarComSeparadores(texto);

        for (var indice = 0;
             indice < partes.Length;
             indice++)
        {
            if (string.Equals(
                    LimparToken(partes[indice]),
                    original,
                    StringComparison.OrdinalIgnoreCase))
            {
                partes[indice] =
                    PreservarCapitalizacao(
                        partes[indice],
                        corrigido);
            }
        }

        return string.Concat(partes);
    }

    private static string[] TokenizarComSeparadores(
        string texto)
    {
        return System.Text.RegularExpressions.Regex.Split(
            texto,
            @"(\s+|[,.;:!?()\[\]])");
    }

    private static string PreservarCapitalizacao(
        string original,
        string corrigido)
    {
        if (original.All(char.IsUpper))
        {
            return corrigido.ToUpperInvariant();
        }

        if (original.Length > 0 &&
            char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(
                       corrigido[0]) +
                   corrigido[1..];
        }

        return corrigido;
    }
}
