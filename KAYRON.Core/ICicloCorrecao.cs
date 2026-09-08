namespace KAYRON.Core;

public interface ICicloCorrecao
{
    Task<ResultadoCicloCorrecao> ExecutarAsync(
        string diretorioProjeto,
        CancellationToken cancellationToken = default);
}
