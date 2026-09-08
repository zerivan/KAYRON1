using System.Text.RegularExpressions;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class Diagnostico
    : IDiagnostico
{
    private static readonly Regex DotNet =
        new(
            @"^(?<arquivo>.+?)\((?<linha>\d+),(?<coluna>\d+)\):\s*(?<severidade>error|warning)\s+(?<codigo>[A-Z]+\d+):\s*(?<mensagem>.+)$",
            RegexOptions.Compiled |
            RegexOptions.IgnoreCase);

    private static readonly Regex DotNetSemColuna =
        new(
            @"^(?<arquivo>.+?)\((?<linha>\d+)\):\s*(?<severidade>error|warning)\s+(?<codigo>[A-Z]+\d+):\s*(?<mensagem>.+)$",
            RegexOptions.Compiled |
            RegexOptions.IgnoreCase);

    private static readonly Regex Node =
        new(
            @"(?<arquivo>[^\s()]+\.(?:js|jsx|ts|tsx|mjs|cjs)):(?<linha>\d+):(?<coluna>\d+)",
            RegexOptions.Compiled |
            RegexOptions.IgnoreCase);

    private static readonly Regex Python =
        new(
            @"File ""(?<arquivo>[^""]+)"", line (?<linha>\d+)",
            RegexOptions.Compiled |
            RegexOptions.IgnoreCase);

    public ResultadoDiagnostico Analisar(
        string saida,
        string erro,
        string origem)
    {
        var texto =
            string.Join(
                Environment.NewLine,
                new[]
                {
                    saida,
                    erro
                }.Where(
                    valor =>
                        !string.IsNullOrWhiteSpace(valor)));

        if (string.IsNullOrWhiteSpace(texto))
        {
            return new ResultadoDiagnostico();
        }

        var problemas =
            new List<DiagnosticoErro>();

        var linhas =
            texto.Split(
                new[]
                {
                    "\r\n",
                    "\n"
                },
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var linha in linhas)
        {
            var correspondencia =
                DotNet.Match(
                    linha.Trim());

            if (correspondencia.Success)
            {
                problemas.Add(
                    CriarDotNet(
                        correspondencia,
                        origem));

                continue;
            }

            correspondencia =
                DotNetSemColuna.Match(
                    linha.Trim());

            if (correspondencia.Success)
            {
                problemas.Add(
                    CriarDotNet(
                        correspondencia,
                        origem));

                continue;
            }

            correspondencia =
                Node.Match(
                    linha);

            if (correspondencia.Success)
            {
                problemas.Add(
                    new DiagnosticoErro
                    {
                        Arquivo =
                            correspondencia.Groups["arquivo"].Value,
                        Linha =
                            int.Parse(
                                correspondencia.Groups["linha"].Value),
                        Coluna =
                            int.Parse(
                                correspondencia.Groups["coluna"].Value),
                        Mensagem =
                            linha.Trim(),
                        Severidade =
                            "erro",
                        Origem =
                            origem
                    });

                continue;
            }

            correspondencia =
                Python.Match(
                    linha);

            if (correspondencia.Success)
            {
                problemas.Add(
                    new DiagnosticoErro
                    {
                        Arquivo =
                            correspondencia.Groups["arquivo"].Value,
                        Linha =
                            int.Parse(
                                correspondencia.Groups["linha"].Value),
                        Mensagem =
                            linha.Trim(),
                        Severidade =
                            "erro",
                        Origem =
                            origem
                    });
            }
        }

        var erros =
            problemas.Count(
                problema =>
                    problema.Severidade.Equals(
                        "error",
                        StringComparison.OrdinalIgnoreCase));

        var avisos =
            problemas.Count(
                problema =>
                    problema.Severidade.Equals(
                        "warning",
                        StringComparison.OrdinalIgnoreCase));

        return new ResultadoDiagnostico
        {
            EncontrouErros =
                erros > 0,
            TotalErros =
                erros,
            TotalAvisos =
                avisos,
            Problemas =
                problemas
        };
    }

    private static DiagnosticoErro CriarDotNet(
        Match correspondencia,
        string origem)
    {
        var grupoColuna =
            correspondencia.Groups["coluna"];

        return new DiagnosticoErro
        {
            Arquivo =
                correspondencia.Groups["arquivo"].Value,
            Linha =
                int.Parse(
                    correspondencia.Groups["linha"].Value),
            Coluna =
                grupoColuna.Success
                    ? int.Parse(
                        grupoColuna.Value)
                    : null,
            Codigo =
                correspondencia.Groups["codigo"].Value,
            Mensagem =
                correspondencia.Groups["mensagem"].Value.Trim(),
            Severidade =
                correspondencia.Groups["severidade"].Value.ToLowerInvariant(),
            Origem =
                origem
        };
    }
}
