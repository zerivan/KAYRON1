namespace KAYRON.Core;

public interface IFerramenta
{
    string Nome { get; }

    string Descricao { get; }

    CapacidadeFerramenta Capacidade { get; }

    Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}
