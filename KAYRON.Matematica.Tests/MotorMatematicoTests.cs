using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

public class MotorMatematicoTests
{
    private static ResultadoMatematico Calcular(string expressao)
    {
        var lexer = new LexerMatematico();
        var parser = new ParserMatematico();
        var motor = new MotorMatematico();

        var tokens = lexer.Tokenizar(expressao);
        var resultadoParser = parser.Parse(tokens);

        Assert.That(resultadoParser.Valido, Is.True);

        return motor.Calcular(resultadoParser.Raiz!, expressao);
    }

    [Test]
    public void DeveCalcularAdicao()
    {
        var resultado = Calcular("2+2+4");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(8m));
    }

    [Test]
    public void DeveRespeitarPrecedencia()
    {
        var resultado = Calcular("120-15*4+60/5");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(72m));
    }

    [Test]
    public void DeveCalcularModulo()
    {
        var casos = new[]
        {
            ("10 % 3", 1m),
            ("20 % 5", 0m),
            ("100 % 7", 2m)
        };

        foreach (var (expressao, esperado) in casos)
        {
            var resultado = Calcular(expressao);

            Assert.That(resultado.Valido, Is.True, expressao);
            Assert.That(resultado.ValorFinal, Is.EqualTo(esperado), expressao);
        }
    }

    [Test]
    public void DeveRespeitarPrecedenciaDoModulo()
    {
        var resultado = Calcular("10 + 20 % 3");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(12m));
    }

    [Test]
    public void DeveGerarEtapaDeModulo()
    {
        var resultado = Calcular("10 % 3");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Etapas, Does.Contain("10 % 3 = 1"));
    }

    [Test]
    public void DeveRejeitarModuloPorZero()
    {
        var resultado = Calcular("10 % 0");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.Erro, Does.Contain("dividir por zero"));
    }

    [Test]
    public void DeveCalcularParenteses()
    {
        var resultado = Calcular("(2+3)*4");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(20m));
    }

    [Test]
    public void DeveCalcularNumeroNegativo()
    {
        var resultado = Calcular("-10+5");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(-5m));
    }

    [Test]
    public void DeveCalcularPotencia()
    {
        var resultado = Calcular("2^3");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(8m));
    }

    [Test]
    public void DeveRespeitarAssociatividadeDaPotencia()
    {
        var resultado = Calcular("2^3^2");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(512m));
    }

    [Test]
    public void DeveCalcularPotenciaComExpoenteNegativo()
    {
        var resultado = Calcular("2^-2");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.ValorFinal, Is.EqualTo(0.25m));
    }

    [Test]
    public void DeveRejeitarDivisaoPorZero()
    {
        var resultado = Calcular("10/0");

        Assert.That(resultado.Valido, Is.False);
        Assert.That(resultado.Erro, Does.Contain("dividir por zero"));
    }

    [Test]
    public void DeveGerarEtapasDoCalculo()
    {
        var resultado = Calcular("2+3*4");

        Assert.That(resultado.Valido, Is.True);
        Assert.That(resultado.Etapas, Is.Not.Empty);
        Assert.That(
            resultado.Etapas,
            Does.Contain("3 * 4 = 12"));
        Assert.That(
            resultado.Etapas,
            Does.Contain("2 + 12 = 14"));
    }

    [Test]
    public void DevePreservarExpressaoOriginal()
    {
        const string expressao = "(10+5)*2";
        var resultado = Calcular(expressao);

        Assert.That(resultado.ExpressaoOriginal, Is.EqualTo(expressao));
    }
}
