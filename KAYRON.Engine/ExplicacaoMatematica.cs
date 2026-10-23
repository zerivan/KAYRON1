namespace KAYRON.Engine;

public sealed class ExplicacaoMatematica
{
    public string ExpressaoOriginal { get; }
    public string Texto { get; }
    public decimal Resultado { get; }
    public bool Valido { get; }

    public ExplicacaoMatematica(
        string expressaoOriginal,
        string texto,
        decimal resultado,
        bool valido)
    {
        ExpressaoOriginal = expressaoOriginal;
        Texto = texto;
        Resultado = resultado;
        Valido = valido;
    }
}
