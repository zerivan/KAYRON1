using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class DetectorIntencaoParentesesTests
{
    private sealed class ContextoTeste : IContexto
    {
        private readonly Dictionary<string, string> _dados = new();

        public void Adicionar(string chave, string valor)
        {
            _dados[chave] = valor;
        }

        public string? Obter(string chave)
        {
            return _dados.TryGetValue(chave, out var valor)
                ? valor
                : null;
        }
    }

    [Test]
    public void DeveDetectarExplique10DivididoPorZero()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("explique 10/0", new ContextoTeste());

        TestContext.WriteLine($"Ferramenta={decisao.Ferramenta}");
        TestContext.WriteLine($"Operacao={decisao.Operacao}");
        TestContext.WriteLine($"Identificada={decisao.Identificada}");
        TestContext.WriteLine($"Evidencia={decisao.Evidencia}");
        TestContext.WriteLine($"Confianca={decisao.Confianca}");

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("10/0"));
    }

    [Test]
    public void DeveDetectarPassoAPassoSemParenteses()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "mostre o passo a passo de 120 ÷ 5 + 18",
            new ContextoTeste());

        TestContext.WriteLine($"Ferramenta={decisao.Ferramenta}");
        TestContext.WriteLine($"Operacao={decisao.Operacao}");
        TestContext.WriteLine($"Identificada={decisao.Identificada}");
        TestContext.WriteLine($"Evidencia={decisao.Evidencia}");
        TestContext.WriteLine($"Confianca={decisao.Confianca}");

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("120 ÷ 5 + 18"));
    }

    [Test]
    public void DeveReconhecerPedidoComParenteses()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "mostre o passo a passo de (((((1+2)))))",
            new ContextoTeste());

        TestContext.WriteLine($"Ferramenta={decisao.Ferramenta}");
        TestContext.WriteLine($"Operacao={decisao.Operacao}");
        TestContext.WriteLine($"Identificada={decisao.Identificada}");
        TestContext.WriteLine($"Evidencia={decisao.Evidencia}");
        TestContext.WriteLine($"Confianca={decisao.Confianca}");

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("(((((1+2)))))"));
    }

    [Test]
    public void DeveReconhecerExpressoesComParenteses()
    {
        var detector = new DetectorIntencao();

        var simples = detector.Detectar("(1+2)", new ContextoTeste());
        var aninhada = detector.Detectar("((3×4)+5)", new ContextoTeste());

        Assert.That(simples.Identificada, Is.True);
        Assert.That(simples.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(simples.Operacao, Is.EqualTo("calcular"));

        Assert.That(aninhada.Identificada, Is.True);
        Assert.That(aninhada.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(aninhada.Operacao, Is.EqualTo("calcular"));
    }

    [Test]
    public void DeveReconhecerExplicacaoComParentesesSimples()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "explique (1+2)",
            new ContextoTeste());

        TestContext.WriteLine($"Ferramenta={decisao.Ferramenta}");
        TestContext.WriteLine($"Operacao={decisao.Operacao}");
        TestContext.WriteLine($"Identificada={decisao.Identificada}");
        TestContext.WriteLine($"Evidencia={decisao.Evidencia}");
        TestContext.WriteLine($"Confianca={decisao.Confianca}");

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("(1+2)"));
    }

    [Test]
    public void DeveRejeitarTextoComumSemExpressaoMatematica()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("passo a passo", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.False);
        Assert.That(decisao.Ferramenta, Is.Empty);
        Assert.That(decisao.Operacao, Is.Empty);
    }

    [Test]
    public void DeveRejeitarTextoComOperadoresMasSemEstruturaMatematica()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("abc+def", new ContextoTeste());

        TestContext.WriteLine($"Ferramenta={decisao.Ferramenta}");
        TestContext.WriteLine($"Operacao={decisao.Operacao}");
        TestContext.WriteLine($"Identificada={decisao.Identificada}");
        TestContext.WriteLine($"Evidencia={decisao.Evidencia}");
        TestContext.WriteLine($"Confianca={decisao.Confianca}");

        Assert.That(decisao.Identificada, Is.False);
    }
}
