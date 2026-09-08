namespace KAYRON.Core;

public sealed class AnaliseCodigo
{
    public bool Encontrado { get; init; }
    public string Arquivo { get; init; } = string.Empty;
    public int? Linha { get; init; }
    public string Trecho { get; init; } = string.Empty;
    public string Contexto { get; init; } = string.Empty;
    public string Diagnostico { get; init; } = string.Empty;
    public string AcaoSugerida { get; init; } = string.Empty;
}
