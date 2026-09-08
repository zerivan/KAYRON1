using KAYRON.Core;
using System.Diagnostics;

namespace KAYRON.Engine;

public sealed class FerramentaProcessos : IFerramenta
{
    public string Nome => "processos";

    public string Descricao =>
        "Consulta processos em execução no sistema.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "listar",
                "consultar"
            ],
            Exemplos =
            [
                "mostre os processos em execução",
                "liste os processos do computador"
            ]
        };
    public Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var processos = Process
            .GetProcesses()
            .OrderBy(p => p.ProcessName)
            .Select(p =>
            {
                try
                {
                    return $"{p.Id} - {p.ProcessName}";
                }
                catch
                {
                    return $"{p.Id} - [indisponível]";
                }
            })
            .ToArray();

        var limite = 100;

        var resultado = processos
            .Take(limite)
            .ToArray();

        var conteudo =
            $"Processos encontrados: {processos.Length}" +
            Environment.NewLine +
            string.Join(
                Environment.NewLine,
                resultado);

        if (processos.Length > limite)
        {
            conteudo +=
                Environment.NewLine +
                $"Exibidos apenas os primeiros {limite}.";
        }

        return Task.FromResult(
            ResultadoFerramenta.Ok(conteudo));
    }
}

