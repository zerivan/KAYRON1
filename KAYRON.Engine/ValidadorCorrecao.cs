using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ValidadorCorrecao
{
    private readonly IProjetoExecutor _projetoExecutor;

    public ValidadorCorrecao(
        IProjetoExecutor projetoExecutor)
    {
        ArgumentNullException.ThrowIfNull(projetoExecutor);

        _projetoExecutor = projetoExecutor;
    }

    public async Task<ResultadoProjeto> ValidarAsync(
        string diretorioProjeto,
        CancellationToken cancellationToken = default)
    {
        return await _projetoExecutor.ExecutarAsync(
            "build",
            diretorioProjeto,
            cancellationToken);
    }
}
