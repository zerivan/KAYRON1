using System.Text.RegularExpressions;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ResolvedorContexto
    : IResolvedorContexto
{
    private static readonly string[] ReferenciasArquivo =
    [
        "esse arquivo",
        "este arquivo",
        "aquele arquivo",
        "o arquivo"
    ];

    private static readonly string[] ReferenciasResultado =
    [
        "isso",
        "isto",
        "aquilo",
        "esse resultado",
        "este resultado",
        "faça o mesmo",
        "faz o mesmo"
    ];

    public string Resolver(
        string entrada,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return string.Empty;
        }

        var resultado =
            entrada.Trim();

        var resumo =
            contexto.Obter(
                "contexto_conversacional");

        if (string.IsNullOrWhiteSpace(resumo))
        {
            return resultado;
        }

        if (ContemReferencia(
                resultado,
                ReferenciasArquivo))
        {
            var caminho =
                ExtrairUltimoCaminhoArquivo(
                    resumo);

            if (!string.IsNullOrWhiteSpace(caminho))
            {
                resultado =
                    SubstituirReferenciaArquivo(
                        resultado,
                        caminho);

                contexto.Adicionar(
                    "referencia_resolvida",
                    caminho);

                return resultado;
            }
        }

        if (ContemReferencia(
                resultado,
                ReferenciasResultado))
        {
            var ultimaResposta =
                contexto.Obter(
                    "ultima_resposta");

            if (!string.IsNullOrWhiteSpace(
                    ultimaResposta))
            {
                contexto.Adicionar(
                    "referencia_resolvida",
                    "ultima_resposta");

                resultado =
                    $"{resultado}{Environment.NewLine}" +
                    $"Contexto da referência:{Environment.NewLine}" +
                    ultimaResposta;
            }
        }

        return resultado;
    }

    private static bool ContemReferencia(
        string texto,
        IEnumerable<string> referencias)
    {
        return referencias.Any(
            referencia =>
                texto.Contains(
                    referencia,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtrairUltimoCaminhoArquivo(
        string resumo)
    {
        var linhas =
            resumo.Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries);

        for (var indice = linhas.Length - 1;
             indice >= 0;
             indice--)
        {
            var caminho =
                ExtrairCaminho(
                    linhas[indice]);

            if (!string.IsNullOrWhiteSpace(caminho))
            {
                return caminho;
            }
        }

        return null;
    }

    private static string? ExtrairCaminho(
        string texto)
    {
        var correspondencia =
            Regex.Match(
                texto,
                @"(?:[A-Za-z]:\\[^<>:""/|?*]+(?:\\[^<>:""/|?*]+)*\.[A-Za-z0-9]+)",
                RegexOptions.IgnoreCase);

        if (correspondencia.Success)
        {
            return correspondencia.Value.Trim();
        }

        return null;
    }

    private static string SubstituirReferenciaArquivo(
        string texto,
        string caminho)
    {
        foreach (var referencia in ReferenciasArquivo)
        {
            var indice =
                texto.IndexOf(
                    referencia,
                    StringComparison.OrdinalIgnoreCase);

            if (indice < 0)
            {
                continue;
            }

            return
                texto[..indice] +
                $"o arquivo {caminho}" +
                texto[(indice + referencia.Length)..];
        }

        return texto;
    }
}
