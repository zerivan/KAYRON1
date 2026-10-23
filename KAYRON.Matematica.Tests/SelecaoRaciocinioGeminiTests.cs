using System.Reflection;
using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class SelecaoRaciocinioGeminiTests
{
    private static readonly MethodInfo SelecionarNivel = typeof(ModeloInteligenciaGemini)
        .GetMethod("SelecionarNivelRaciocinio", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Método de seleção de raciocínio não encontrado.");

    [TestCase("Oi", "MINIMAL")]
    [TestCase("Crie um prompt para um anúncio de hotel", "MEDIUM")]
    [TestCase("Crie um prompt para a imagem de um hotel com arquitetura elegante", "MEDIUM")]
    [TestCase("Corrigir o código do projeto e analisar a causa raiz", "HIGH")]
    public void SelecionaNivelAdequadoAoTipoDeSolicitacao(string entrada, string nivelEsperado)
    {
        var instrucao = new Instrucao { Conteudo = entrada };
        var contexto = new KAYRON.Engine.Contexto();

        var nivel = SelecionarNivel.Invoke(null, [instrucao, contexto]);

        Assert.That(nivel?.ToString(), Is.EqualTo(nivelEsperado));
    }
}
