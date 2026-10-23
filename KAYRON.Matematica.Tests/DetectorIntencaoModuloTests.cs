using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class DetectorIntencaoModuloTests
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
    public void DeveDetectarModuloComoCalculo()
    {
        var detector = new DetectorIntencao();

        var decisao = detector.Detectar(
            "10 % 3",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("calcular"));
        Assert.That(decisao.Evidencia, Is.EqualTo("10 % 3"));
    }

    [Test]
    public void InterpretadorNaoDeveAlterarExplicacaoDeModulo()
    {
        var contexto = new ContextoTeste();
        var interpretador = new InterpretadorLinguagem(
            new CatalogoFerramentas());

        var resultado = interpretador.Interpretar(
            "explique 10 % 3",
            contexto);

        Assert.That(resultado, Is.EqualTo("explique 10 % 3"));
    }

    [Test]
    public void DeveDetectarExplicacaoDeModulo()
    {
        var detector = new DetectorIntencao();

        var decisao = detector.Detectar(
            "explique 10 % 3",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("10 % 3"));
    }
}
