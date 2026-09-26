using System.Text.RegularExpressions;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class DetectorIntencao : IDetectorIntencao
{
    public IntencaoDetectada Detectar(
        string texto,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var normalizado = Normalizar(texto);

        if (ContemAlgum(normalizado,
                "corrija os erros",
                "corrigir os erros",
                "corrija erro",
                "corrigir erro",
                "corrija o projeto",
                "corrigir o projeto",
                "corrija este projeto",
                "corrigir este projeto",
                "encontre e corrija",
                "encontrar e corrigir",
                "autocorrija",
                "autocorrigir",
                "auto corrija",
                "auto corrigir"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "autocorrecao",
                Operacao = "autocorrigir",
                Confianca = 0.98,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (ContemAlgum(normalizado,
                "pesquise",
                "pesquisar",
                "pesquisa",
                "procure na internet",
                "procure na web",
                "pesquise na internet",
                "pesquise na web",
                "busque na internet",
                "busque na web",
                "qual e o atual",
                "quem e o atual",
                "preco atual",
                "cotacao atual",
                "cotacao do dolar",
                "cotacao dolar",
                "valor do dolar",
                "dolar hoje",
                "dolar agora",
                "preco do dolar",
                "noticias de hoje"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "internet",
                Operacao = "pesquisar",
                Confianca = 0.97,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (ContemAlgum(normalizado,
                "analise o codigo",
                "analisar o codigo",
                "analise este codigo",
                "analisar este codigo",
                "encontre o erro",
                "encontrar o erro",
                "descubra o erro",
                "diagnostique o erro"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "analise-codigo",
                Operacao = "analisar",
                Confianca = 0.95,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (EhMatematica(normalizado))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "matematica",
                Operacao = "calcular",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = texto
            };
        }

        return new IntencaoDetectada
        {
            Ferramenta = string.Empty,
            Operacao = string.Empty,
            Confianca = 0,
            Identificada = false,
            Evidencia = texto
        };
    }

    private static bool EhMatematica(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var expressao = Regex.Match(
            texto,
            @"(?<![A-Za-z0-9_])(\d+(?:[.,]\d+)?)\s*([+\-*/x×÷])\s*(\d+(?:[.,]\d+)?)(?![A-Za-z0-9_])",
            RegexOptions.CultureInvariant);

        if (expressao.Success)
            return true;

        return ContemAlgum(texto,
                "quanto e",
                "quanto eh",
                "calcule",
                "calcular") &&
            expressao.Success;
    }

    private static bool ContemAlgum(
        string texto,
        params string[] termos)
    {
        foreach (var termo in termos)
        {
            if (texto.Contains(
                    termo,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalizar(
        string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var normalizado = texto.Trim().ToLowerInvariant();

        normalizado = normalizado
            .Replace('á', 'a')
            .Replace('à', 'a')
            .Replace('ã', 'a')
            .Replace('â', 'a')
            .Replace('ä', 'a')
            .Replace('é', 'e')
            .Replace('è', 'e')
            .Replace('ê', 'e')
            .Replace('ë', 'e')
            .Replace('í', 'i')
            .Replace('ì', 'i')
            .Replace('î', 'i')
            .Replace('ï', 'i')
            .Replace('ó', 'o')
            .Replace('ò', 'o')
            .Replace('õ', 'o')
            .Replace('ô', 'o')
            .Replace('ö', 'o')
            .Replace('ú', 'u')
            .Replace('ù', 'u')
            .Replace('û', 'u')
            .Replace('ü', 'u')
            .Replace('ç', 'c');

        return normalizado;
    }
}
