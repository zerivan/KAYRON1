namespace KAYRON.Core;

public sealed class SolicitacaoConfirmacao
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Ferramenta { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string Argumentos { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public DateTime CriadaEm { get; init; } = DateTime.UtcNow;
}
