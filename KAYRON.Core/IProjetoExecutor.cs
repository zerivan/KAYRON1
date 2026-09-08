namespace KAYRON.Core;

public interface IProjetoExecutor
{
    Task<ResultadoProjeto> ExecutarAsync(
        string operacao,
        string diretorio,
        CancellationToken cancellationToken = default);
}
