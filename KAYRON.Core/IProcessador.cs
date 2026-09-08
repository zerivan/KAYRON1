namespace KAYRON.Core;

public interface IProcessador
{
    Task<Resposta> ExecutarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}
