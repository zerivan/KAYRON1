namespace KAYRON.Core;

public sealed class ResultadoAutocorrecao
{
    public bool Sucesso { get; init; }
    public string Etapa { get; init; } = string.Empty;
    public string Arquivo { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
}
