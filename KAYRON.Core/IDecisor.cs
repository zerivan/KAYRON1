namespace KAYRON.Core;

public interface IDecisor
{
    Task<Resposta> DecidirAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}
