namespace KAYRON.Core;

public sealed class ResultadoTerminal
{
    public bool Sucesso { get; init; }

    public int CodigoSaida { get; init; }

    public string Saida { get; init; } = string.Empty;

    public string Erro { get; init; } = string.Empty;

    public static ResultadoTerminal Ok(
        int codigoSaida,
        string saida,
        string erro = "")
    {
        return new ResultadoTerminal
        {
            Sucesso = codigoSaida == 0,
            CodigoSaida = codigoSaida,
            Saida = saida,
            Erro = erro
        };
    }

    public static ResultadoTerminal Falha(
        string erro)
    {
        return new ResultadoTerminal
        {
            Sucesso = false,
            CodigoSaida = -1,
            Erro = erro
        };
    }
}
