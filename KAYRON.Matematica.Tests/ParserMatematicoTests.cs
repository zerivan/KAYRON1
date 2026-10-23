using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

public class ParserMatematicoTests
{
    private static ResultadoParser Parse(string expressao)
    {
        var lexer = new LexerMatematico();
        var parser = new ParserMatematico();
        return parser.Parse(lexer.Tokenizar(expressao));
    }

    [Test]
    public void DeveInterpretarAdicao()
    {
        var resultado = Parse("2+2+4");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Raiz, Is.TypeOf<OperacaoBinaria>());

        var raiz = (OperacaoBinaria)resultado.Raiz!;
        Assert.That(raiz.Operador, Is.EqualTo("+"));
        Assert.That(raiz.Esquerda, Is.TypeOf<OperacaoBinaria>());
        Assert.That(raiz.Direita, Is.TypeOf<Numero>());
    }

    [Test]
    public void DeveRespeitarPrecedenciaDeMultiplicacao()
    {
        var resultado = Parse("2+3*4");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (OperacaoBinaria)resultado.Raiz!;
        Assert.That(raiz.Operador, Is.EqualTo("+"));
        Assert.That(raiz.Direita, Is.TypeOf<OperacaoBinaria>());

        var multiplicacao = (OperacaoBinaria)raiz.Direita;
        Assert.That(multiplicacao.Operador, Is.EqualTo("*"));
    }

    [Test]
    public void DeveInterpretarModuloComMesmaPrecedenciaDeMultiplicacaoEDivisao()
    {
        var resultado = Parse("10+20%3");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (OperacaoBinaria)resultado.Raiz!;
        Assert.That(raiz.Operador, Is.EqualTo("+"));
        Assert.That(raiz.Direita, Is.TypeOf<OperacaoBinaria>());

        var modulo = (OperacaoBinaria)raiz.Direita;
        Assert.That(modulo.Operador, Is.EqualTo("%"));
    }

    [Test]
    public void DeveInterpretarModuloEmSequenciaComMultiplicacao()
    {
        var resultado = Parse("100%7*2");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (OperacaoBinaria)resultado.Raiz!;
        Assert.That(raiz.Operador, Is.EqualTo("*"));
        Assert.That(raiz.Esquerda, Is.TypeOf<OperacaoBinaria>());

        var modulo = (OperacaoBinaria)raiz.Esquerda;
        Assert.That(modulo.Operador, Is.EqualTo("%"));
    }

    [Test]
    public void DeveInterpretarParenteses()
    {
        var resultado = Parse("(2+3)*4");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (OperacaoBinaria)resultado.Raiz!;
        Assert.That(raiz.Operador, Is.EqualTo("*"));
        Assert.That(raiz.Esquerda, Is.TypeOf<Agrupamento>());
    }

    [Test]
    public void DeveInterpretarUnarioNegativo()
    {
        var resultado = Parse("-10+5");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (OperacaoBinaria)resultado.Raiz!;
        Assert.That(raiz.Esquerda, Is.TypeOf<OperacaoUnaria>());

        var unario = (OperacaoUnaria)raiz.Esquerda;
        Assert.That(unario.Operador, Is.EqualTo("-"));
        Assert.That(unario.Operando, Is.TypeOf<Numero>());
    }

    [Test]
    public void DeveInterpretarPotencia()
    {
        var resultado = Parse("2^3");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Raiz, Is.TypeOf<Potencia>());
    }

    [Test]
    public void PotenciaDeveSerAssociativaADireita()
    {
        var resultado = Parse("2^3^2");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (Potencia)resultado.Raiz!;
        Assert.That(raiz.Expoente, Is.TypeOf<Potencia>());
    }

    [Test]
    public void DeveInterpretarPotenciaComParenteses()
    {
        var resultado = Parse("2^(3+1)");

        Assert.That(resultado.Valido, Is.True);

        var raiz = (Potencia)resultado.Raiz!;
        Assert.That(raiz.Expoente, Is.TypeOf<Agrupamento>());
    }

    [Test]
    public void DeveRejeitarParentesesNaoFechados()
    {
        var resultado = Parse("(2+3");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.MensagemErro, Does.Contain("Parêntese"));
    }

    [Test]
    public void DeveRejeitarSequenciaDeOperadores()
    {
        var resultado = Parse("2++*3");

        Assert.That(resultado.Valido, Is.False);
    }

    [Test]
    public void DeveRejeitarTokenDepoisDaExpressao()
    {
        var resultado = Parse("2 3");

        Assert.That(resultado.Valido, Is.False);
    }
}
