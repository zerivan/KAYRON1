using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class DetectorIntencaoTests
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
    public void DeveDetectarCalculoSimples()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("2+2+4", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("calcular"));
    }

    [Test]
    public void DeveDetectarPedidoExplicacaoSimples()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("explique 2+2+4", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("2+2+4"));
    }

    [Test]
    public void DeveDetectarPedidoExplicacaoComPassoAPasso()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "mostre o passo a passo de 120 ÷ 5 + 18",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("120 ÷ 5 + 18"));
    }

    [Test]
    public void DeveDetectarErroMatematicoSemExplicador()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("10/0", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("calcular"));
    }

    [Test]
    public void DeveDetectarErroMatematicoComExplicador()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("explique 10/0", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("10/0"));
    }

    [Test]
    public void DeveDetectarModulo()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("10 % 3", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("calcular"));
    }

    [Test]
    public void DeveDetectarExplicacaoDeModulo()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar("explique 10 % 3", new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("matematica"));
        Assert.That(decisao.Operacao, Is.EqualTo("explicar"));
        Assert.That(decisao.Evidencia, Is.EqualTo("10 % 3"));
    }

    [Test]
    public void DeveEncaminharPerguntaSobrePresidenteAtualParaPesquisa()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "Quem é o presidente atual do Brasil?",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("internet"));
        Assert.That(decisao.Operacao, Is.EqualTo("pesquisar"));
    }

    [Test]
    public void DeveEncaminharPerguntaSobreDeputadosEleitosParaPesquisa()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "Quais são os deputados federais eleitos no Maranhão?",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("internet"));
        Assert.That(decisao.Operacao, Is.EqualTo("pesquisar"));
    }

    [Test]
    public void DeveManterPerguntaConversacionalForaDaPesquisa()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "Quem é você?",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.False);
    }

    [Test]
    public void NaoDeveClassificarTextoNaoMatematico()
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            "explique o funcionamento do computador",
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.False);
    }
}
