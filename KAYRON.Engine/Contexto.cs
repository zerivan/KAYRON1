using KAYRON.Core;

namespace KAYRON.Engine;

public class Contexto : IContexto
{
    private readonly Dictionary<string, string> _dados =
        new(StringComparer.OrdinalIgnoreCase);

    public void Adicionar(string chave, string valor)
    {
        _dados[chave] = valor;
    }

    public string? Obter(string chave)
    {
        return _dados.TryGetValue(chave, out var valor)
            ? valor
            : null;
    }
}
