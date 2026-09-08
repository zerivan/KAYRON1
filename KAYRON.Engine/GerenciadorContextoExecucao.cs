using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class GerenciadorContextoExecucao
    : IContextoExecucao
{
    private readonly List<ContextoExecucao> _historico = [];

    private readonly object _lock = new();

    private ContextoExecucao? _atual;

    public ContextoExecucao Atual
    {
        get
        {
            lock (_lock)
            {
                return _atual
                    ?? throw new InvalidOperationException(
                        "Nenhuma execução está ativa.");
            }
        }
    }

    public IReadOnlyCollection<ContextoExecucao> Historico
    {
        get
        {
            lock (_lock)
            {
                return _historico.ToArray();
            }
        }
    }

    public ContextoExecucao Iniciar(
        string instrucao)
    {
        if (string.IsNullOrWhiteSpace(instrucao))
        {
            throw new ArgumentException(
                "A instrução não pode estar vazia.",
                nameof(instrucao));
        }

        var contexto =
            new ContextoExecucao
            {
                Instrucao = instrucao.Trim()
            };

        lock (_lock)
        {
            _atual = contexto;
            _historico.Add(contexto);
        }

        return contexto;
    }
}
