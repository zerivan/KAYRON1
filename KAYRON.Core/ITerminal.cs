namespace KAYRON.Core;

public interface ITerminal
{
    Task<ResultadoTerminal> ExecutarAsync(
        string comando,
        string? diretorio = null,
        CancellationToken cancellationToken = default);
}
