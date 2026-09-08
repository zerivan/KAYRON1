using KAYRON.Core;
using System.Runtime.InteropServices;

namespace KAYRON.Engine;

public sealed class FerramentaSistema : IFerramenta
{
    public string Nome => "sistema";

    public string Descricao =>
        "Obtém informações do sistema operacional e ambiente.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "informações do sistema",
                "ambiente"
            ],
            Exemplos =
            [
                "mostre as informações do computador",
                "qual sistema operacional está sendo usado"
            ]
        };
    public Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var conteudo =
            $"Sistema operacional: {RuntimeInformation.OSDescription}" +
            Environment.NewLine +
            $"Arquitetura: {RuntimeInformation.OSArchitecture}" +
            Environment.NewLine +
            $"Processador: {Environment.ProcessorCount} núcleos" +
            Environment.NewLine +
            $"Framework: {RuntimeInformation.FrameworkDescription}" +
            Environment.NewLine +
            $"Máquina: {Environment.MachineName}" +
            Environment.NewLine +
            $"Diretório atual: {Environment.CurrentDirectory}";

        return Task.FromResult(
            ResultadoFerramenta.Ok(conteudo));
    }
}

