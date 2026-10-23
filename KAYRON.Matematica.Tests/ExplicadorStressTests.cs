using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

public sealed class ExplicadorStressTests
{
    private static (ResultadoMatematico Resultado, ExplicacaoMatematica Explicacao) Executar(string expressao)
    {
        var lexer = new LexerMatematico();
        var parser = new ParserMatematico();
        var motor = new MotorMatematico();
        var explicador = new ExplicadorMatematico();

        var resultadoLexer = lexer.Tokenizar(expressao);
        Assert.That(resultadoLexer.Valido, Is.True, resultadoLexer.MensagemErro);

        var resultadoParser = parser.Parse(resultadoLexer);
        Assert.That(resultadoParser.Valido, Is.True, resultadoParser.MensagemErro);
        Assert.That(resultadoParser.Raiz, Is.Not.Null);

        var resultado = motor.Calcular(resultadoParser.Raiz!, expressao);
        var explicacao = explicador.Explicar(resultado);

        return (resultado, explicacao);
    }

    [Test]
    public void DeveSuportar10OperacoesSimples()
    {
        var (resultado, explicacao) = Executar("1+2+3+4+5+6+7+8+9+10");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(55m));
            Assert.That(resultado.Etapas, Has.Count.EqualTo(9));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("=== Passo a passo: ==="));
            Assert.That(explicacao.Texto, Does.Contain("Resultado final: 55"));
        });
    }

    [Test]
    public void DeveSuportar25OperacoesMistas()
    {
        var (resultado, explicacao) = Executar("100+20×3-15÷5+8×2-6+40÷4-7×2+30-5×3+18÷2-9+11×2-14+36÷6-8×3+25-10÷2+4×5-12+21÷3");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(203m));
            Assert.That(resultado.Etapas, Is.Not.Empty);
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("=== Ordem de cálculo ===").Or.Contain("Ordem de cálculo:"));
            Assert.That(explicacao.Texto, Does.Contain("=== Passo a passo: ==="));
            Assert.That(explicacao.Texto, Does.Contain("Resultado final: 203"));
        });
    }

    [Test]
    public void DeveSuportar50OperacoesComParenteses()
    {
        var partes = Enumerable.Range(1, 25)
            .Select(i => $"(({i}+{i + 1})×2)");

        var expressao = string.Join("+", partes);
        var esperado = Enumerable.Range(1, 25)
            .Sum(i => (i + i + 1) * 2);

        var (resultado, explicacao) = Executar(expressao);

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(esperado));
            Assert.That(resultado.Etapas, Is.Not.Empty);
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("Ordem de cálculo:"));
            Assert.That(explicacao.Texto, Does.Contain("1. Parênteses"));
            Assert.That(explicacao.Texto, Does.Contain("=== Passo a passo: ==="));
        });
    }

    [Test]
    public void DeveSuportar100Operacoes()
    {
        var expressao = string.Join("+", Enumerable.Range(1, 101));

        var (resultado, explicacao) = Executar(expressao);

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(5151m));
            Assert.That(resultado.Etapas, Has.Count.EqualTo(100));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("Etapa 100:"));
            Assert.That(explicacao.Texto, Does.Contain("Resultado final: 5151"));
        });
    }

    [Test]
    public void DeveSuportar10NiveisDeParenteses()
    {
        var expressao = new string('(', 10) + "1+2" + new string(')', 10);

        var (resultado, explicacao) = Executar(expressao);

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(3m));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("1. Parênteses"));
            Assert.That(explicacao.Texto, Does.Contain("Resultado final: 3"));
        });
    }
}
