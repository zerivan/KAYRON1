namespace KAYRON.Core;

public interface IDecisor
{
    Task<DecisaoAgente> DecidirAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}
