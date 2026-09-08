namespace KAYRON.Core;

public sealed class DiagnosticoErro
{
    public string Arquivo { get; init; } = string.Empty;
    public int? Linha { get; init; }
    public int? Coluna { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
    public string Severidade { get; init; } = string.Empty;
    public string Origem { get; init; } = string.Empty;
}
