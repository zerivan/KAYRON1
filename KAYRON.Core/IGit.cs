namespace KAYRON.Core;

public interface IGit
{
    Task<ResultadoGit> ExecutarAsync(
        string argumentos,
        string diretorio,
        CancellationToken cancellationToken = default);
}
