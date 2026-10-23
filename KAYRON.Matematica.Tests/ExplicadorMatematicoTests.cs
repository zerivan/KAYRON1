using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class ExplicadorMatematicoTests
{
    [Test]
    public void Explicador_DeveGerarExplicacaoPassoAPasso()
    {
        var resultado = new ResultadoMatematico(
            8,
            new List<string> { "2+2=4", "4+4=8" },
            "2+2+4",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.True);
        Assert.That(explicacao.ExpressaoOriginal, Is.EqualTo("2+2+4"));
        Assert.That(explicacao.Texto, Does.Contain("Passo a passo:"));
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 8"));
    }

    [Test]
    public void Explicador_DeveListarTodasAsEtapas()
    {
        var etapas = new List<string>
        {
            "1200 ÷ 5 = 240",
            "18 × 7 = 126",
            "45 ÷ 3 = 15",
            "16 × 2 = 32",
            "240 + 126 = 366",
            "366 - 15 = 351",
            "351 + 32 = 383"
        };

        var resultado = new ResultadoMatematico(
            383,
            etapas,
            "1200 ÷ 5 + 18 × 7 - 45 ÷ 3 + 16 × 2",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.True);

        foreach (var etapa in etapas)
            Assert.That(explicacao.Texto, Does.Contain(etapa));

        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 383"));
    }

    [Test]
    public void Explicador_DeveExibirErroDeDivisaoPorZeroSemEtapas()
    {
        var resultado = new ResultadoMatematico(
            0,
            new List<string>(),
            "10/0",
            false,
            "Erro: divisão por zero");

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.False);
        Assert.That(explicacao.Texto, Does.Contain("Erro: divisão por zero"));
        Assert.That(explicacao.Texto, Does.Not.Contain("Passo a passo"));
    }

    [Test]
    public void Explicador_DeveExibirErroDeParentesesIncorretos()
    {
        var resultado = new ResultadoMatematico(
            0,
            new List<string>(),
            "(2+3",
            false,
            "Erro: parênteses incorretos");

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.False);
        Assert.That(explicacao.Texto, Does.Contain("Erro: parênteses incorretos"));
    }

    [Test]
    public void Explicador_DeveExibirResultadoSemPassoAPassoQuandoNaoHaEtapas()
    {
        var resultado = new ResultadoMatematico(
            5,
            new List<string>(),
            "2+3",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.True);
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 5"));
        Assert.That(explicacao.Texto, Does.Not.Contain("Passo a passo"));
    }

    [Test]
    public void Explicador_DeveExplicarPrecedenciaDeDivisaoAntesDaSoma()
    {
        var resultado = new ResultadoMatematico(
            42,
            new List<string>
            {
                "120 ÷ 5 = 24",
                "24 + 18 = 42"
            },
            "120 ÷ 5 + 18",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Multiplicação e divisão"));
        Assert.That(explicacao.Texto, Does.Contain("Adição e subtração"));
        Assert.That(explicacao.Texto, Does.Contain("Dividimos o primeiro valor pelo segundo."));
        Assert.That(explicacao.Texto, Does.Contain("Somamos os valores."));
    }

    [Test]
    public void Explicador_DeveExplicarModulo()
    {
        var resultado = new ResultadoMatematico(
            1,
            new List<string>
            {
                "10 % 3 = 1"
            },
            "10 % 3",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.True);
        Assert.That(
            explicacao.Texto,
            Does.Contain("Resto da divisão"));
        Assert.That(
            explicacao.Texto,
            Does.Contain("10 % 3 = 1"));
        Assert.That(
            explicacao.Texto,
            Does.Contain("Calculamos o resto da divisão entre os valores."));
        Assert.That(
            explicacao.Texto,
            Does.Contain("Resultado final: 1"));
    }

    [Test]
    public void Explicador_DeveExplicarParentesesEPotencia()
    {
        var resultado = new ResultadoMatematico(
            125,
            new List<string>
            {
                "3 + 2 = 5",
                "5 ^ 3 = 125"
            },
            "(3+2)^3",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Parênteses"));
        Assert.That(explicacao.Texto, Does.Contain("Potências"));
        Assert.That(explicacao.Texto, Does.Contain("Elevamos a base ao expoente."));
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 125"));
    }

    [Test]
    public void Explicador_DeveExplicarOperacoesDeMultiplicacaoESubtracao()
    {
        var resultado = new ResultadoMatematico(
            14,
            new List<string>
            {
                "3 × 4 = 12",
                "20 - 6 = 14"
            },
            "3 × 4 + 20 - 6",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Multiplicamos os fatores."));
        Assert.That(explicacao.Texto, Does.Contain("Subtraímos o segundo valor do primeiro."));
    }

    [Test]
    public void Explicador_DeveExplicarOperacaoUnariaNegativa()
    {
        var resultado = new ResultadoMatematico(
            -5,
            new List<string>
            {
                "5 → -5"
            },
            "-5",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Aplicamos o sinal negativo ao valor."));
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: -5"));
    }

    [Test]
    public void Explicador_DeveExplicarParentesesAninhados()
    {
        var resultado = new ResultadoMatematico(
            14,
            new List<string>
            {
                "3 + 4 = 7",
                "2 + 7 = 9",
                "9 + 5 = 14"
            },
            "2+(3+4)+5",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Parênteses"));
        Assert.That(explicacao.Texto, Does.Contain("3 + 4 = 7"));
        Assert.That(explicacao.Texto, Does.Contain("2 + 7 = 9"));
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 14"));
    }

    [Test]
    public void Explicador_DeveExplicarPotenciaAssociadaADireita()
    {
        var resultado = new ResultadoMatematico(
            512,
            new List<string>
            {
                "3 ^ 2 = 9",
                "2 ^ 9 = 512"
            },
            "2^3^2",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Potências"));
        Assert.That(explicacao.Texto, Does.Contain("3 ^ 2 = 9"));
        Assert.That(explicacao.Texto, Does.Contain("2 ^ 9 = 512"));
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 512"));
    }

    [Test]
    public void Explicador_DeveExplicarMisturaDeParenteses()
    {
        var resultado = new ResultadoMatematico(
            64,
            new List<string>
            {
                "10 - 2 = 8",
                "5 + 3 = 8",
                "8 × 8 = 64"
            },
            "(10-2)×(5+3)",
            true);

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Texto, Does.Contain("Parênteses"));
        Assert.That(explicacao.Texto, Does.Contain("Multiplicação e divisão"));
        Assert.That(explicacao.Texto, Does.Contain("10 - 2 = 8"));
        Assert.That(explicacao.Texto, Does.Contain("5 + 3 = 8"));
        Assert.That(explicacao.Texto, Does.Contain("8 × 8 = 64"));
        Assert.That(explicacao.Texto, Does.Contain("Resultado final: 64"));
    }

    [Test]
    public void Explicador_DevePreservarErroDeSintaxeSemInventarEtapas()
    {
        var resultado = new ResultadoMatematico(
            0,
            new List<string>(),
            "2+*3",
            false,
            "Erro: sintaxe matemática inválida");

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.False);
        Assert.That(explicacao.Texto, Is.EqualTo("Erro: sintaxe matemática inválida"));
        Assert.That(explicacao.Texto, Does.Not.Contain("Passo a passo"));
        Assert.That(explicacao.Texto, Does.Not.Contain("Resultado final"));
    }

    [Test]
    public void Explicador_DevePreservarErroDeDivisaoPorZeroSemInventarEtapas()
    {
        var resultado = new ResultadoMatematico(
            0,
            new List<string>(),
            "10/0",
            false,
            "Não é possível dividir por zero.");

        var explicador = new ExplicadorMatematico();
        var explicacao = explicador.Explicar(resultado);

        Assert.That(explicacao.Valido, Is.False);
        Assert.That(explicacao.Texto, Is.EqualTo("Não é possível dividir por zero."));
        Assert.That(explicacao.Texto, Does.Not.Contain("Passo a passo"));
    }
}
