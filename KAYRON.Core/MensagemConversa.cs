namespace KAYRON.Core;

public sealed class MensagemConversa
{
    public DateTimeOffset DataHora { get; init; } =
        DateTimeOffset.Now;

    public string Papel { get; init; } = string.Empty;

    public string Conteudo { get; init; } = string.Empty;
}
