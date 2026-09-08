namespace KAYRON.Core;

public interface IModeloInteligencia
{
    Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default);
}
