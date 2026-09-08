namespace KAYRON.Core;

public interface IAnalisadorCodigo
{
    Task<AnaliseCodigo> AnalisarAsync(
        DiagnosticoErro erro,
        string diretorioProjeto,
        CancellationToken cancellationToken = default);
}
