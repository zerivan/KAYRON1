namespace KAYRON.Core;

public sealed class ResultadoGit
{
    public bool Sucesso { get; init; }
    public string Saida { get; init; } = string.Empty;
    public string Erro { get; init; } = string.Empty;
    public int CodigoSaida { get; init; }

    public static ResultadoGit Ok(
        string saida,
        int codigoSaida = 0) =>
        new()
        {
            Sucesso = true,
            Saida = saida,
            CodigoSaida = codigoSaida
        };

    public static ResultadoGit Falha(
        string erro,
        int codigoSaida = -1) =>
        new()
        {
            Sucesso = false,
            Erro = erro,
            CodigoSaida = codigoSaida
        };
}
