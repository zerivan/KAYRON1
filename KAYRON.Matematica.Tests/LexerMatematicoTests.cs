using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class LexerMatematicoTests
{
    private readonly LexerMatematico _lexer = new();

    [Test]
    public void DeveTokenizarOperadoresBasicos()
    {
        var resultado = _lexer.Tokenizar("2+3*4/5");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Tokens.Select(t => t.Tipo), Is.EqualTo(new[]
        {
            TipoToken.Numero,
            TipoToken.Mais,
            TipoToken.Numero,
            TipoToken.Multiplicacao,
            TipoToken.Numero,
            TipoToken.Divisao,
            TipoToken.Numero,
            TipoToken.Fim
        }));
    }

    [Test]
    public void DeveAceitarMultiplicacaoComSimbolos()
    {
        foreach (var expressao in new[] { "2*3", "2×3", "2x3" })
        {
            var resultado = _lexer.Tokenizar(expressao);

            Assert.That(resultado.Valido, Is.True, expressao);
            Assert.That(resultado.Tokens[1].Tipo, Is.EqualTo(TipoToken.Multiplicacao));
        }
    }

    [Test]
    public void DeveAceitarDivisaoComSimbolos()
    {
        foreach (var expressao in new[] { "6/2", "6÷2" })
        {
            var resultado = _lexer.Tokenizar(expressao);

            Assert.That(resultado.Valido, Is.True, expressao);
            Assert.That(resultado.Tokens[1].Tipo, Is.EqualTo(TipoToken.Divisao));
        }
    }

    [Test]
    public void DeveTokenizarPotenciaEParenteses()
    {
        var resultado = _lexer.Tokenizar("2^(3+1)");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Tokens.Select(t => t.Tipo), Is.EqualTo(new[]
        {
            TipoToken.Numero,
            TipoToken.Potencia,
            TipoToken.AbreParenteses,
            TipoToken.Numero,
            TipoToken.Mais,
            TipoToken.Numero,
            TipoToken.FechaParenteses,
            TipoToken.Fim
        }));
    }

    [Test]
    public void DeveAceitarDecimalComVirgula()
    {
        var resultado = _lexer.Tokenizar("2,5+3,7");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Tokens[0].Valor, Is.EqualTo(2.5m));
        Assert.That(resultado.Tokens[2].Valor, Is.EqualTo(3.7m));
    }

    [Test]
    public void DeveAceitarDecimalComPonto()
    {
        var resultado = _lexer.Tokenizar("2.5+3.7");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Tokens[0].Valor, Is.EqualTo(2.5m));
        Assert.That(resultado.Tokens[2].Valor, Is.EqualTo(3.7m));
    }

    [Test]
    public void DeveIgnorarEspacos()
    {
        var resultado = _lexer.Tokenizar("  2 + 3 * ( 4 - 1 )  ");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Tokens.Count, Is.EqualTo(10));
    }

    [Test]
    public void DeveRejeitarDecimalComVirgulaDuplicada()
    {
        var resultado = _lexer.Tokenizar("2,,5");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.TipoErro, Is.EqualTo(TipoErroLexer.DecimalInvalido));
        Assert.That(resultado.MensagemErro, Is.EqualTo("Erro: decimal inválido"));
    }

    [Test]
    public void DeveRejeitarDecimalComPontoDuplicado()
    {
        var resultado = _lexer.Tokenizar("2..5");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.TipoErro, Is.EqualTo(TipoErroLexer.DecimalInvalido));
    }

    [Test]
    public void DeveRejeitarDecimalComSeparadoresMisturados()
    {
        var resultado = _lexer.Tokenizar("2,5.7");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.TipoErro, Is.EqualTo(TipoErroLexer.DecimalInvalido));
    }

    [Test]
    public void DeveRejeitarCaractereInvalido()
    {
        var resultado = _lexer.Tokenizar("2+@+3");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.TipoErro, Is.EqualTo(TipoErroLexer.CaractereInvalido));
        Assert.That(resultado.MensagemErro, Is.EqualTo("Erro: caractere inválido"));
    }

    [Test]
    public void DeveTokenizarModulo()
    {
        var resultado = _lexer.Tokenizar("10 % 3");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Tokens.Select(t => t.Tipo), Is.EqualTo(new[]
        {
            TipoToken.Numero,
            TipoToken.Modulo,
            TipoToken.Numero,
            TipoToken.Fim
        }));
    }

    [Test]
    public void DeveRejeitarExpressaoVazia()
    {
        var resultado = _lexer.Tokenizar("   ");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.MensagemErro, Is.EqualTo("Erro: expressão inválida"));
    }
}
