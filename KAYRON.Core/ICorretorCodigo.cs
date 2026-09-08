namespace KAYRON.Core;

public interface ICorretorCodigo
{
    Task<ResultadoCorrecao> AplicarAsync(
        AlteracaoCodigo alteracao,
        CancellationToken cancellationToken = default);
}
