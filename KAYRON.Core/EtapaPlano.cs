namespace KAYRON.Core;

public sealed class EtapaPlano
{
    public int Ordem { get; init; }

    public string Descricao { get; init; } = string.Empty;

    public string Ferramenta { get; init; } = string.Empty;

    public string Argumentos { get; init; } = string.Empty;
}
