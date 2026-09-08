namespace KAYRON.Core;

public sealed class AlteracaoCodigo
{
    public string Arquivo { get; init; } = string.Empty;
    public string ConteudoOriginal { get; init; } = string.Empty;
    public string ConteudoNovo { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
}
