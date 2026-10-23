namespace KAYRON.Engine;

public enum TipoToken
{
    Numero,
    Mais,
    Menos,
    Multiplicacao,
    Divisao,
    Modulo,
    Potencia,
    AbreParenteses,
    FechaParenteses,
    Fim
}

public sealed class Token
{
    public TipoToken Tipo { get; }
    public string Texto { get; }
    public decimal? Valor { get; }
    public int Posicao { get; }

    public Token(TipoToken tipo, string texto, decimal? valor, int posicao)
    {
        Tipo = tipo;
        Texto = texto;
        Valor = valor;
        Posicao = posicao;
    }
}

public enum TipoErroLexer
{
    Nenhum,
    CaractereInvalido,
    NumeroInvalido,
    DecimalInvalido
}

public sealed class ResultadoLexer
{
    public List<Token> Tokens { get; }
    public bool Valido { get; }
    public int? PosicaoErro { get; }
    public string? MensagemErro { get; }
    public TipoErroLexer TipoErro { get; }

    public ResultadoLexer(
        List<Token> tokens,
        bool valido,
        int? posicaoErro = null,
        string? mensagemErro = null,
        TipoErroLexer tipoErro = TipoErroLexer.Nenhum)
    {
        Tokens = tokens;
        Valido = valido;
        PosicaoErro = posicaoErro;
        MensagemErro = mensagemErro;
        TipoErro = tipoErro;
    }
}
