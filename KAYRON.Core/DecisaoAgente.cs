namespace KAYRON.Core;

public enum TipoAcaoAgente
{
    Conversar,
    ExecutarFerramenta,
    SolicitarInformacao,
    Concluir
}

public sealed class DecisaoAgente
{
    public TipoAcaoAgente Acao { get; init; }

    public string Ferramenta { get; init; } = string.Empty;

    public string Operacao { get; init; } = string.Empty;

    public string Motivo { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Parametros { get; init; }
        = new Dictionary<string, string>();
}
