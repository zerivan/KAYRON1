using System.Globalization;
using System.Text;

namespace KAYRON.Engine;

public sealed class LexerMatematico
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public ResultadoLexer Tokenizar(string expressao)
    {
        if (string.IsNullOrWhiteSpace(expressao))
        {
            return Erro(TipoErroLexer.NumeroInvalido, 0, "Erro: expressão inválida");
        }

        var tokens = new List<Token>();
        var i = 0;

        while (i < expressao.Length)
        {
            var caractere = expressao[i];

            if (char.IsWhiteSpace(caractere))
            {
                i++;
                continue;
            }

            if (char.IsDigit(caractere))
            {
                var inicio = i;
                var separador = false;
                var digitosDepoisSeparador = 0;

                while (i < expressao.Length && char.IsDigit(expressao[i]))
                {
                    i++;
                }

                if (i < expressao.Length && (expressao[i] == ',' || expressao[i] == '.'))
                {
                    separador = true;
                    i++;

                    while (i < expressao.Length && char.IsDigit(expressao[i]))
                    {
                        digitosDepoisSeparador++;
                        i++;
                    }

                    if (digitosDepoisSeparador == 0)
                    {
                        return Erro(TipoErroLexer.DecimalInvalido, i, "Erro: decimal inválido");
                    }

                    if (i < expressao.Length && (expressao[i] == ',' || expressao[i] == '.'))
                    {
                        return Erro(TipoErroLexer.DecimalInvalido, i, "Erro: decimal inválido");
                    }
                }

                if (!separador && i < expressao.Length && (expressao[i] == ',' || expressao[i] == '.'))
                {
                    return Erro(TipoErroLexer.DecimalInvalido, i, "Erro: decimal inválido");
                }

                var textoNumero = expressao[inicio..i];
                var textoConversao = textoNumero.Replace('.', ',');

                if (!decimal.TryParse(
                        textoConversao,
                        NumberStyles.Number,
                        Cultura,
                        out var valor))
                {
                    return Erro(TipoErroLexer.NumeroInvalido, inicio, "Erro: número inválido");
                }

                tokens.Add(new Token(TipoToken.Numero, textoNumero, valor, inicio));
                continue;
            }

            var token = caractere switch
            {
                '+' => new Token(TipoToken.Mais, caractere.ToString(), null, i),
                '-' => new Token(TipoToken.Menos, caractere.ToString(), null, i),
                '*' => new Token(TipoToken.Multiplicacao, caractere.ToString(), null, i),
                '×' => new Token(TipoToken.Multiplicacao, caractere.ToString(), null, i),
                'x' => new Token(TipoToken.Multiplicacao, caractere.ToString(), null, i),
                '/' => new Token(TipoToken.Divisao, caractere.ToString(), null, i),
                '÷' => new Token(TipoToken.Divisao, caractere.ToString(), null, i),
                '%' => new Token(TipoToken.Modulo, caractere.ToString(), null, i),
                '^' => new Token(TipoToken.Potencia, caractere.ToString(), null, i),
                '(' => new Token(TipoToken.AbreParenteses, caractere.ToString(), null, i),
                ')' => new Token(TipoToken.FechaParenteses, caractere.ToString(), null, i),
                _ => null
            };

            if (token is null)
            {
                return Erro(TipoErroLexer.CaractereInvalido, i, "Erro: caractere inválido");
            }

            tokens.Add(token);
            i++;
        }

        tokens.Add(new Token(TipoToken.Fim, string.Empty, null, expressao.Length));
        return new ResultadoLexer(tokens, true);
    }

    private static ResultadoLexer Erro(
        TipoErroLexer tipo,
        int posicao,
        string mensagem)
    {
        return new ResultadoLexer(
            new List<Token>(),
            false,
            posicao,
            mensagem,
            tipo);
    }
}
