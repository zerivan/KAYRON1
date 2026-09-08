using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class RegistroFerramentas
{
    private readonly Dictionary<string, IFerramenta> _ferramentas =
        new(StringComparer.OrdinalIgnoreCase);

    public void Registrar(IFerramenta ferramenta)
    {
        ArgumentNullException.ThrowIfNull(ferramenta);

        if (string.IsNullOrWhiteSpace(ferramenta.Nome))
        {
            throw new ArgumentException(
                "A ferramenta precisa possuir um nome.",
                nameof(ferramenta));
        }

        _ferramentas[ferramenta.Nome.Trim()] = ferramenta;
    }

    public IFerramenta? Obter(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return null;
        }

        return _ferramentas.TryGetValue(
            nome.Trim(),
            out var ferramenta)
            ? ferramenta
            : null;
    }

    public IReadOnlyCollection<IFerramenta> Listar()
    {
        return _ferramentas.Values.ToArray();
    }
}
