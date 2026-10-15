namespace KAYRON.Engine;

public sealed class ResultadoMatematico
{
    public decimal ValorFinal { get; }
    public List<string> Etapas { get; }
    public string ExpressaoOriginal { get; }
    public bool Valido { get; }
    public string? Erro { get; }

    public ResultadoMatematico(
        decimal valorFinal,
        List<string> etapas,
        string expressaoOriginal,
        bool valido,
        string? erro = null)
    {
        ValorFinal = valorFinal;
        Etapas = etapas;
        ExpressaoOriginal = expressaoOriginal;
        Valido = valido;
        Erro = erro;
    }
}
