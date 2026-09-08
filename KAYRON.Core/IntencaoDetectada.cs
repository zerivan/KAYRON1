namespace KAYRON.Core;

public sealed class IntencaoDetectada
{
    public string Ferramenta { get; init; } = string.Empty;

    public string Operacao { get; init; } = string.Empty;

    public double Confianca { get; init; }

    public bool Identificada { get; init; }

    public string? Evidencia { get; init; }
}
