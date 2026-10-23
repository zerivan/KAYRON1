using System;
using System.Threading;
using System.Threading.Tasks;
using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class ConversorUnidadesTests
{
    private readonly IContexto contexto = new ContextoTeste();

    [TestCase("10 km em metros", 10000d)]
    [TestCase("5 m em centímetros", 500d)]
    [TestCase("250 cm em metros", 2.5d)]
    [TestCase("1 km em milímetros", 1000000d)]
    public async Task Distancia_DeveConverterCorretamente(string entrada, double esperado)
    {
        var conversor = new ConversorUnidades();

        var resultado = await conversor.ExecutarAsync(
            entrada,
            contexto,
            CancellationToken.None);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(double.Parse(resultado.Conteudo, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(esperado).Within(0.000001));
    }

    [TestCase("1 kg em gramas", 1000d)]
    [TestCase("2500 g em kg", 2.5d)]
    [TestCase("500 mg em g", 0.5d)]
    public async Task Massa_DeveConverterCorretamente(string entrada, double esperado)
    {
        var conversor = new ConversorUnidades();

        var resultado = await conversor.ExecutarAsync(
            entrada,
            contexto,
            CancellationToken.None);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(double.Parse(resultado.Conteudo, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(esperado).Within(0.000001));
    }

    [TestCase("2 horas em minutos", 120d)]
    [TestCase("120 minutos em horas", 2d)]
    [TestCase("90 segundos em minutos", 1.5d)]
    public async Task Tempo_DeveConverterCorretamente(string entrada, double esperado)
    {
        var conversor = new ConversorUnidades();

        var resultado = await conversor.ExecutarAsync(
            entrada,
            contexto,
            CancellationToken.None);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(double.Parse(resultado.Conteudo, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(esperado).Within(0.000001));
    }

    [TestCase("100 °C em °F", 212d)]
    [TestCase("32 °F em °C", 0d)]
    [TestCase("0 °C em K", 273.15d)]
    public async Task Temperatura_DeveConverterCorretamente(string entrada, double esperado)
    {
        var conversor = new ConversorUnidades();

        var resultado = await conversor.ExecutarAsync(
            entrada,
            contexto,
            CancellationToken.None);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(double.Parse(resultado.Conteudo, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(esperado).Within(0.000001));
    }

    [TestCase("72 km/h em m/s", 20d)]
    [TestCase("10 m/s em km/h", 36d)]
    public async Task Velocidade_DeveConverterCorretamente(string entrada, double esperado)
    {
        var conversor = new ConversorUnidades();

        var resultado = await conversor.ExecutarAsync(
            entrada,
            contexto,
            CancellationToken.None);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(double.Parse(resultado.Conteudo, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(esperado).Within(0.000001));
    }

    [TestCase("10 abc em metros")]
    [TestCase("10 km em xyz")]
    [TestCase("10 kg em metros")]
    [TestCase("abc km em metros")]
    public async Task EntradasInvalidasOuIncompativeis_DeveRetornarFalha(string entrada)
    {
        var conversor = new ConversorUnidades();

        var resultado = await conversor.ExecutarAsync(
            entrada,
            contexto,
            CancellationToken.None);

        Assert.That(resultado.Sucesso, Is.False);
        Assert.That(resultado.Erro, Is.Not.Null.And.Not.Empty);
    }

    private sealed class ContextoTeste : IContexto
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _dados = new();

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
}
