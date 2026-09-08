using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaInfo : IFerramenta
{
    public string Nome => "info";

    public string Descricao =>
        "Retorna informações básicas sobre o mecanismo KAYRON.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "informações",
                "status"
            ],
            Exemplos =
            [
                "quem é você",
                "qual o status do KAYRON"
            ]
        };

    public Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var quantidade =
            contexto.Obter("ferramentas_registradas")
            ?? "não informado";

        var conteudo =
            "KAYRON Engine ativo." +
            Environment.NewLine +
            $"Ferramentas registradas: {quantidade}";

        return Task.FromResult(
            ResultadoFerramenta.Ok(conteudo));
    }
}
