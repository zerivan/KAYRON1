namespace KAYRON.Core;

public sealed class ResultadoPlano
{
    public bool Sucesso { get; init; }

    public string Objetivo { get; init; } = string.Empty;

    public IReadOnlyCollection<Resposta> Resultados { get; init; } =
        Array.Empty<Resposta>();

    public string? Erro { get; init; }

    public static ResultadoPlano Ok(
        string objetivo,
        IReadOnlyCollection<Resposta> resultados)
    {
        return new ResultadoPlano
        {
            Sucesso = true,
            Objetivo = objetivo,
            Resultados = resultados
        };
    }

    public static ResultadoPlano Falha(
        string objetivo,
        string erro,
        IReadOnlyCollection<Resposta> resultados)
    {
        return new ResultadoPlano
        {
            Sucesso = false,
            Objetivo = objetivo,
            Erro = erro,
            Resultados = resultados
        };
    }
}
