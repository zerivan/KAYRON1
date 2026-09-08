namespace KAYRON.Core;

public sealed class InformacoesProjeto
{
    public bool Identificado { get; init; }

    public string Diretorio { get; init; } = string.Empty;

    public string? ArquivoProjeto { get; init; }

    public string? Tipo { get; init; }

    public string? Solucao { get; init; }
}
