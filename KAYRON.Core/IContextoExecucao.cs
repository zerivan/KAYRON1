namespace KAYRON.Core;

public interface IContextoExecucao
{
    ContextoExecucao Iniciar(
        string instrucao);

    ContextoExecucao Atual { get; }

    IReadOnlyCollection<ContextoExecucao> Historico { get; }
}
