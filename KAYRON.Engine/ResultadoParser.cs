namespace KAYRON.Engine;

public sealed class ResultadoParser
{
    public NoAst? Raiz { get; }
    public bool Valido { get; }
    public int? PosicaoErro { get; }
    public string? MensagemErro { get; }

    public ResultadoParser(
        NoAst? raiz,
        bool valido,
        int? posicaoErro = null,
        string? mensagemErro = null)
    {
        Raiz = raiz;
        Valido = valido;
        PosicaoErro = posicaoErro;
        MensagemErro = mensagemErro;
    }
}
