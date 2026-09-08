namespace KAYRON.Core;

public sealed class ResultadoFerramenta
{
    public bool Sucesso { get; init; }

    public string Conteudo { get; init; } = string.Empty;

    public string? Erro { get; init; }

    public static ResultadoFerramenta Ok(string conteudo)
    {
        return new ResultadoFerramenta
        {
            Sucesso = true,
            Conteudo = conteudo
        };
    }

    public static ResultadoFerramenta Falha(string erro)
    {
        return new ResultadoFerramenta
        {
            Sucesso = false,
            Erro = erro
        };
    }
}
