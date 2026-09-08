using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class CatalogoCapacidades
{
    private readonly ICatalogoFerramentas _catalogo;

    public CatalogoCapacidades(
        ICatalogoFerramentas catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        _catalogo = catalogo;
    }

    public IReadOnlyCollection<CapacidadeFerramenta> Listar()
    {
        return _catalogo
            .Listar()
            .Select(ferramenta => ferramenta.Capacidade)
            .ToArray();
    }

    public CapacidadeFerramenta? Obter(
        string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return null;
        }

        return _catalogo
            .Obter(nome)
            ?.Capacidade;
    }
}
