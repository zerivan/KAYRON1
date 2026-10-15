using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KAYRON.Core;
using KAYRON.Engine;
using NUnit.Framework;
using Microsoft.Extensions.Logging.Abstractions;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class ConversorUnidadesIntegracaoTests
{
    [TestCase("10 km em metros", "10000")]
    [TestCase("1 kg para gramas", "1000")]
    [TestCase("100 °C em Fahrenheit", "212")]
    [TestCase("72 km/h para m/s", "20")]
    public async Task DeveExecutarConversaoPeloPipelineCompleto(
        string entrada,
        string esperado)
    {
        var resultado = await ExecutarPipelineAsync(entrada);

        Assert.That(resultado.Sucesso, Is.True);
        Assert.That(resultado.Conteudo, Is.EqualTo(esperado));
    }

    [Test]
    public async Task DeveRetornarFalhaDeCategoriasIncompativeisPeloPipelineCompleto()
    {
        var resultado = await ExecutarPipelineAsync("10 kg para metros");

        Assert.That(resultado.Sucesso, Is.False);
        Assert.That(resultado.Erro, Is.Not.Null.And.Not.Empty);
    }

    private static async Task<ResultadoFerramenta> ExecutarPipelineAsync(
        string entrada)
    {
        var contexto = new ContextoTeste();
        var detector = new DetectorIntencao();

        var intencao = detector.Detectar(
            entrada,
            contexto);

        Assert.That(intencao.Identificada, Is.True);
        Assert.That(intencao.Ferramenta, Is.EqualTo("conversao"));
        Assert.That(intencao.Operacao, Is.EqualTo("converter"));

        contexto.Adicionar(
            "intencao_detectada",
            $"{intencao.Ferramenta}:{intencao.Operacao}");

        contexto.Adicionar(
            "intencao_evidencia",
            intencao.Evidencia ?? entrada);

        var decisor = new Decisor(
            (ConvocadorMemoria)RuntimeHelpers.GetUninitializedObject(
                typeof(ConvocadorMemoria)),
            NullLogger<Decisor>.Instance);

        var decisao = await decisor.DecidirAsync(
            new Instrucao { Conteudo = entrada },
            contexto,
            CancellationToken.None);

        Assert.That(
            decisao.Acao,
            Is.EqualTo(TipoAcaoAgente.ExecutarFerramenta));

        Assert.That(
            decisao.Ferramenta,
            Is.EqualTo("conversao"));

        Assert.That(
            decisao.Operacao,
            Is.EqualTo("converter"));

        var catalogo = new CatalogoFerramentas();
        catalogo.Registrar(new ConversorUnidades());

        var executor = new ExecutorFerramentas(
            catalogo,
            new AutorizadorStub(),
            new ConfirmadorStub());

        var argumentos = decisao.Parametros["instrucao"];

        return await executor.ExecutarAsync(
            decisao.Ferramenta,
            argumentos,
            contexto,
            CancellationToken.None,
            decisao.Operacao);
    }

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

    private sealed class AutorizadorStub : IAutorizadorFerramentas
    {
        public ResultadoAutorizacao Autorizar(
            IFerramenta ferramenta,
            string argumentos,
            string? operacaoSolicitada = null)
        {
            return ResultadoAutorizacao.Permitir();
        }
    }

    private sealed class ConfirmadorStub : IConfirmadorOperacao
    {
        public SolicitacaoConfirmacao Criar(
            string ferramenta,
            string operacao,
            string argumentos,
            string motivo,
            IContexto contexto)
        {
            return new SolicitacaoConfirmacao
            {
                Ferramenta = ferramenta,
                Operacao = operacao,
                Argumentos = argumentos,
                Motivo = motivo
            };
        }

        public bool Confirmar(string id, IContexto contexto) => true;

        public bool Cancelar(string id, IContexto contexto) => true;

        public SolicitacaoConfirmacao? ObterPendente(IContexto contexto) => null;
    }
}
