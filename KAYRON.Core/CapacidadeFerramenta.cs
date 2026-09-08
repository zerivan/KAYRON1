namespace KAYRON.Core;

public sealed class CapacidadeFerramenta
{
    public string Nome { get; init; } = string.Empty;

    public string Descricao { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Operacoes { get; init; } =
        Array.Empty<string>();

    public IReadOnlyCollection<string> Exemplos { get; init; } =
        Array.Empty<string>();
}
