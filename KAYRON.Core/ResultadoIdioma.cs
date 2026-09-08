namespace KAYRON.Core;

public sealed class ResultadoIdioma
{
    public Idioma Idioma { get; init; } = new();

    public double Confianca { get; init; }

    public bool Identificado { get; init; }
}
