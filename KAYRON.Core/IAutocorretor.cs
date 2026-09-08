namespace KAYRON.Core;

public interface IAutocorretor
{
    Task<ResultadoAutocorrecao> ExecutarAsync(
        string diretorioProjeto,
        CancellationToken cancellationToken = default);
}
