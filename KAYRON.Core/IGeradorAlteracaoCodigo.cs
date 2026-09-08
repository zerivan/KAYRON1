namespace KAYRON.Core;

public interface IGeradorAlteracaoCodigo
{
    Task<AlteracaoCodigo?> GerarAsync(
        AnaliseCodigo analise,
        CancellationToken cancellationToken = default);
}
