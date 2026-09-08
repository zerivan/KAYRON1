using KAYRON.Core;

namespace KAYRON.Engine;

public class DispatcherComandos
{
    private readonly Dictionary<string, IComando> _comandos =
        new(StringComparer.OrdinalIgnoreCase);

    public void Registrar(IComando comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        _comandos[comando.Nome] = comando;
    }

    public IComando? Obter(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return null;
        }

        return _comandos.TryGetValue(nome.Trim(), out var comando)
            ? comando
            : null;
    }
}
