namespace KAYRON.Core;

public sealed class ResultadoResolucaoConversa
{
    public string? Resposta { get; init; }

    public IntencaoDetectada? Intencao { get; init; }

    public bool TemResposta =>
        !string.IsNullOrWhiteSpace(Resposta);

    public bool TemIntencao =>
        Intencao?.Identificada == true;
}
