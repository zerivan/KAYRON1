namespace KAYRON.Core;

public interface IExecutorPlano
{
    Task<ResultadoPlano> ExecutarAsync(
        PlanoExecucao plano,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}
