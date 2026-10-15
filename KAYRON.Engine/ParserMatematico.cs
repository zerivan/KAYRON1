namespace KAYRON.Engine;

public sealed class ParserMatematico
{
    private List<Token> _tokens = new();
    private int _indice;

    public ResultadoParser Parse(ResultadoLexer resultadoLexer)
    {
        if (resultadoLexer == null)
            throw new ArgumentNullException(nameof(resultadoLexer));

        if (!resultadoLexer.Valido)
        {
            return new ResultadoParser(
                null,
                false,
                resultadoLexer.PosicaoErro,
                resultadoLexer.MensagemErro ?? "Erro léxico na expressão.");
        }

        _tokens = resultadoLexer.Tokens;
        _indice = 0;

        try
        {
            var raiz = Expressao();

            if (Atual.Tipo != TipoToken.Fim)
                return ErroAtual("Token inesperado.");

            return new ResultadoParser(raiz, true);
        }
        catch (ParserException ex)
        {
            return new ResultadoParser(
                null,
                false,
                ex.PosicaoErro,
                ex.Message);
        }
    }

    private NoAst Expressao()
    {
        return Soma();
    }

    private NoAst Soma()
    {
        var esquerda = Produto();

        while (Atual.Tipo == TipoToken.Mais || Atual.Tipo == TipoToken.Menos)
        {
            var operador = Consumir().Texto;
            var direita = Produto();
            esquerda = new OperacaoBinaria(operador, esquerda, direita);
        }

        return esquerda;
    }

    private NoAst Produto()
    {
        var esquerda = Potencia();

        while (Atual.Tipo == TipoToken.Multiplicacao ||
               Atual.Tipo == TipoToken.Divisao ||
               Atual.Tipo == TipoToken.Modulo)
        {
            var operador = Consumir().Texto;
            var direita = Potencia();
            esquerda = new OperacaoBinaria(operador, esquerda, direita);
        }

        return esquerda;
    }

    private NoAst Potencia()
    {
        var esquerda = Unario();

        if (Atual.Tipo == TipoToken.Potencia)
        {
            var operador = Consumir().Texto;
            var direita = Potencia();
            return new Potencia(esquerda, direita);
        }

        return esquerda;
    }

    private NoAst Unario()
    {
        if (Atual.Tipo == TipoToken.Mais || Atual.Tipo == TipoToken.Menos)
        {
            var operador = Consumir().Texto;
            var operando = Unario();
            return new OperacaoUnaria(operador, operando);
        }

        return Primario();
    }

    private NoAst Primario()
    {
        if (Atual.Tipo == TipoToken.Numero)
        {
            var token = Consumir();

            if (!token.Valor.HasValue)
                throw new ParserException(
                    token.Posicao,
                    "Número sem valor válido.");

            return new Numero(token.Valor.Value);
        }

        if (Atual.Tipo == TipoToken.AbreParenteses)
        {
            Consumir();
            var expressao = Expressao();

            if (Atual.Tipo != TipoToken.FechaParenteses)
                throw new ParserException(
                    Atual.Posicao,
                    "Parêntese de fechamento esperado.");

            Consumir();
            return new Agrupamento(expressao);
        }

        throw new ParserException(
            Atual.Posicao,
            "Número ou '(' esperado.");
    }

    private Token Atual =>
        _indice < _tokens.Count
            ? _tokens[_indice]
            : new Token(TipoToken.Fim, string.Empty, null, _indice);

    private Token Consumir()
    {
        var token = Atual;

        if (_indice < _tokens.Count)
            _indice++;

        return token;
    }

    private ResultadoParser ErroAtual(string mensagem)
    {
        var token = Atual;

        return new ResultadoParser(
            null,
            false,
            token.Posicao,
            mensagem);
    }

    private sealed class ParserException : Exception
    {
        public int PosicaoErro { get; }

        public ParserException(int posicaoErro, string mensagem)
            : base(mensagem)
        {
            PosicaoErro = posicaoErro;
        }
    }
}
