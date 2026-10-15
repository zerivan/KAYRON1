using KAYRON.Engine;
using NUnit.Framework;
using System.Diagnostics;

namespace KAYRON.Matematica.Tests;

public sealed class ExplicadorStressTests2
{
    private static (ResultadoMatematico Resultado, ExplicacaoMatematica Explicacao, long Milissegundos) Executar(string expressao)
    {
        var cronometro = Stopwatch.StartNew();

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

        cronometro.Stop();

        return (resultado, explicacao, cronometro.ElapsedMilliseconds);
    }

    private static string CriarExpressaoMista(int quantidadeTermos)
    {
        var partes = new List<string> { "1000" };

        for (var i = 1; i < quantidadeTermos; i++)
        {
            var operador = (i % 4) switch
            {
                1 => "+",
                2 => "-",
                3 => "×",
                _ => "÷"
            };

            var valor = operador == "÷" ? "2" : ((i % 9) + 1).ToString();
            partes.Add(operador);
            partes.Add(valor);
        }

        return string.Concat(partes);
    }

    private static string CriarParentesesAninhados(int niveis)
    {
        return new string('(', niveis) + "1+2" + new string(')', niveis);
    }

    [Test]
    public void DeveSuportar250Operacoes()
    {
        var expressao = CriarExpressaoMista(251);

        var (resultado, explicacao, milissegundos) = Executar(expressao);

        TestContext.WriteLine($"250 operações — {milissegundos} ms — {explicacao.Texto.Length} caracteres.");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.Etapas, Has.Count.EqualTo(250));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("Etapa 250:"));
            Assert.That(explicacao.Texto, Does.Contain($"Resultado final: {resultado.ValorFinal}"));
        });
    }

    [Test]
    public void DeveSuportar500Operacoes()
    {
        var expressao = CriarExpressaoMista(501);

        var (resultado, explicacao, milissegundos) = Executar(expressao);

        TestContext.WriteLine($"500 operações — {milissegundos} ms — {explicacao.Texto.Length} caracteres.");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.Etapas, Has.Count.EqualTo(500));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("Etapa 500:"));
            Assert.That(explicacao.Texto, Does.Contain($"Resultado final: {resultado.ValorFinal}"));
        });
    }

    [Test]
    public void DeveSuportar20NiveisDeParenteses()
    {
        var expressao = CriarParentesesAninhados(20);

        var (resultado, explicacao, milissegundos) = Executar(expressao);

        TestContext.WriteLine($"20 níveis — {milissegundos} ms — {explicacao.Texto.Length} caracteres.");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(3m));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("1. Parênteses"));
            Assert.That(explicacao.Texto, Does.Contain("Resultado final: 3"));
        });
    }

    [Test]
    public void DeveSuportar30NiveisDeParenteses()
    {
        var expressao = CriarParentesesAninhados(30);

        var (resultado, explicacao, milissegundos) = Executar(expressao);

        TestContext.WriteLine($"30 níveis — {milissegundos} ms — {explicacao.Texto.Length} caracteres.");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(resultado.ValorFinal, Is.EqualTo(3m));
            Assert.That(explicacao.Valido, Is.True);
            Assert.That(explicacao.Texto, Does.Contain("1. Parênteses"));
            Assert.That(explicacao.Texto, Does.Contain("Resultado final: 3"));
        });
    }

    [Test]
    public void DeveAvaliarLegibilidadeEmCenariosExtremos()
    {
        var expressao = CriarExpressaoMista(501);

        var (resultado, explicacao, milissegundos) = Executar(expressao);

        var quantidadeEtapas = resultado.Etapas.Count;
        var tamanhoTexto = explicacao.Texto.Length;

        TestContext.WriteLine($"Legibilidade — {quantidadeEtapas} etapas — {tamanhoTexto} caracteres — {milissegundos} ms.");

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Valido, Is.True);
            Assert.That(quantidadeEtapas, Is.EqualTo(500));
            Assert.That(tamanhoTexto, Is.GreaterThan(0));
            Assert.That(explicacao.Texto, Does.Contain("=== Expressão original ==="));
            Assert.That(explicacao.Texto, Does.Contain("=== Passo a passo: ==="));
            Assert.That(explicacao.Texto, Does.Contain("=== Resultado final ==="));
            Assert.That(explicacao.Texto, Does.Contain("Etapa 500:"));
        });
    }
}
