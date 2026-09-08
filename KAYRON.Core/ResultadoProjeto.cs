namespace KAYRON.Core;

public sealed class ResultadoProjeto
{
    public bool Sucesso { get; init; }
    public string Operacao { get; init; } = string.Empty;
    public string TipoProjeto { get; init; } = string.Empty;
    public string Saida { get; init; } = string.Empty;
    public string Erro { get; init; } = string.Empty;
    public int CodigoSaida { get; init; }

    public static ResultadoProjeto Ok(
        string operacao,
        string tipoProjeto,
        string saida,
        int codigoSaida = 0) =>
        new()
        {
            Sucesso = true,
            Operacao = operacao,
            TipoProjeto = tipoProjeto,
            Saida = saida,
            CodigoSaida = codigoSaida
        };

    public static ResultadoProjeto Falha(
        string operacao,
        string tipoProjeto,
        string erro,
        int codigoSaida = -1) =>
        new()
        {
            Sucesso = false,
            Operacao = operacao,
            TipoProjeto = tipoProjeto,
            Erro = erro,
            CodigoSaida = codigoSaida
        };
}
