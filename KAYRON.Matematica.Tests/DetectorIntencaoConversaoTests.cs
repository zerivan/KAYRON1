using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class DetectorIntencaoConversaoTests
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

    [TestCase("10 km em metros")]
    [TestCase("10 km para metros")]
    [TestCase("converter 10 km para metros")]
    [TestCase("converta 10 km para metros")]
    [TestCase("quanto dá 10 km em metros")]
    [TestCase("3,5 km em metros")]
    [TestCase("72 km/h para m/s")]
    [TestCase("100 °C em Fahrenheit")]
    [TestCase("1 kg para gramas")]
    public void DeveDetectarConversao(
        string entrada)
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            entrada,
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.True);
        Assert.That(decisao.Ferramenta, Is.EqualTo("conversao"));
        Assert.That(decisao.Operacao, Is.EqualTo("converter"));
    }

    [TestCase("o carro percorreu 10 km ontem")]
    [TestCase("a temperatura chegou a 40 °C")]
    [TestCase("o pacote pesa 2 kg")]
    public void NaoDeveClassificarTextoComUnidadeSemPedidoDeConversao(
        string entrada)
    {
        var detector = new DetectorIntencao();
        var decisao = detector.Detectar(
            entrada,
            new ContextoTeste());

        Assert.That(decisao.Identificada, Is.False);
    }
}
