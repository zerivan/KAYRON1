namespace KAYRON.Core;

public interface IGerenciadorTarefas
{
    Task<Resposta> ExecutarAsync(
        string objetivo,
        IContexto contexto,
        CancellationToken cancellationToken = default,
        IntencaoDetectada? intencao = null);
}
