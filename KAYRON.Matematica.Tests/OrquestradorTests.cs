using System.Runtime.CompilerServices;
using KAYRON.Core;
using KAYRON.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace KAYRON.Matematica.Tests;

[TestFixture]
public class OrquestradorTests
{
    private Orquestrador _orquestrador = null!;
    private IContexto _contexto = null!;

    [SetUp]
    public void Setup()
    {
        var catalogo = new CatalogoFerramentas();
        var autorizador = new AutorizadorStub();
        var confirmador = new ConfirmadorStub();
        var executor = new ExecutorFerramentas(
            catalogo,
            autorizador,
            confirmador);

        var decisor = new DecisorStub();
        var modelo = new ModeloInteligenciaStub();
        var memoria = new MemoriaAprendidaStub();

        // GestorConhecimento depende de classes concretas e sealed
        // que inicializam infraestrutura externa. Para estes testes
        // matemáticos ele não é exercitado; criamos uma instância inerte.
        var gestor = CriarGestorConhecimentoInerte();

        var logger = NullLogger<Orquestrador>.Instance;

        _orquestrador = new Orquestrador(
            executor,
            catalogo,
            decisor,
            modelo,
            gestor,
            memoria,
            logger);

        _contexto = new KAYRON.Engine.Contexto();
    }

    [Test]
    public async Task DeveCalcularExpressaoSimples()
    {
        var resposta = await _orquestrador.ProcessarAsync(
            new Instrucao { Conteudo = "2+2+4" },
            _contexto);

        Assert.That(resposta.Conteudo, Does.Contain("8"));
    }

    [Test]
    public async Task DeveExplicarExpressaoSimples()
    {
        var resposta = await _orquestrador.ProcessarIntencaoAsync(
            new Instrucao { Conteudo = "explique 2+2+4" },
            _contexto,
            new IntencaoDetectada
            {
                Ferramenta = "matematica",
                Operacao = "explicar",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = "2+2+4"
            });

        Assert.That(resposta.Conteudo, Does.Contain("Passo a passo"));
        Assert.That(resposta.Conteudo, Does.Contain("8"));
    }

    [Test]
    public async Task DeveExplicarExpressaoComPassoAPasso()
    {
        var resposta = await _orquestrador.ProcessarIntencaoAsync(
            new Instrucao { Conteudo = "mostre o passo a passo de 120 ÷ 5 + 18" },
            _contexto,
            new IntencaoDetectada
            {
                Ferramenta = "matematica",
                Operacao = "explicar",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = "120 ÷ 5 + 18"
            });

        Assert.That(resposta.Conteudo, Does.Contain("Passo a passo"));
        Assert.That(resposta.Conteudo, Does.Contain("120 ÷ 5"));
        Assert.That(resposta.Conteudo, Does.Contain("42"));
    }

    [Test]
    public async Task DeveCalcularModulo()
    {
        var resposta = await _orquestrador.ProcessarAsync(
            new Instrucao { Conteudo = "10 % 3" },
            _contexto);

        Assert.That(resposta.Conteudo, Does.Contain("1"));
    }

    [Test]
    public async Task DeveExplicarModulo()
    {
        var resposta = await _orquestrador.ProcessarIntencaoAsync(
            new Instrucao { Conteudo = "explique 10 % 3" },
            _contexto,
            new IntencaoDetectada
            {
                Ferramenta = "matematica",
                Operacao = "explicar",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = "10 % 3"
            });

        Assert.That(resposta.Conteudo, Does.Contain("10 % 3"));
        Assert.That(resposta.Conteudo, Does.Contain("1"));
        Assert.That(
            resposta.Conteudo,
            Does.Contain("resto da divisão"));
    }

    [Test]
    public async Task DeveProcessarExplicacaoDeModuloPeloFluxoNormal()
    {
        _contexto.Adicionar(
            "intencao_detectada",
            "matematica:explicar");

        _contexto.Adicionar(
            "intencao_evidencia",
            "10 % 3");

        var resposta = await _orquestrador.ProcessarAsync(
            new Instrucao { Conteudo = "explique 10 % 3" },
            _contexto);

        Assert.That(resposta.Conteudo, Does.Contain("Passo a passo"));
        Assert.That(resposta.Conteudo, Does.Contain("10 % 3"));
        Assert.That(resposta.Conteudo, Does.Contain("1"));
    }

    [Test]
    public async Task DeveRetornarErroDeDivisaoPorZero()
    {
        var resposta = await _orquestrador.ProcessarAsync(
            new Instrucao { Conteudo = "10/0" },
            _contexto);

        Assert.That(resposta.Conteudo, Does.Contain("divisão por zero"));
        Assert.That(resposta.Conteudo, Does.Not.Contain("Passo a passo"));
    }

    [Test]
    public async Task DeveExplicarErroDeDivisaoPorZero()
    {
        var resposta = await _orquestrador.ProcessarIntencaoAsync(
            new Instrucao { Conteudo = "explique 10/0" },
            _contexto,
            new IntencaoDetectada
            {
                Ferramenta = "matematica",
                Operacao = "explicar",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = "10/0"
            });

        Assert.That(resposta.Conteudo, Does.Contain("dividir por zero"));
    }

    private static GestorConhecimento CriarGestorConhecimentoInerte()
    {
        return (GestorConhecimento)RuntimeHelpers.GetUninitializedObject(
            typeof(GestorConhecimento));
    }

    private sealed class DecisorStub : IDecisor
    {
        public Task<DecisaoAgente> DecidirAsync(
            Instrucao instrucao,
            IContexto contexto,
            CancellationToken cancellationToken = default)
        {
            var intencao = contexto.Obter("intencao_detectada");

            if (string.Equals(
                    intencao,
                    "matematica:calcular",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new DecisaoAgente
                {
                    Acao = TipoAcaoAgente.ExecutarFerramenta,
                    Ferramenta = "matematica",
                    Operacao = "calcular"
                });
            }

            if (string.Equals(
                    intencao,
                    "matematica:explicar",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new DecisaoAgente
                {
                    Acao = TipoAcaoAgente.ExecutarFerramenta,
                    Ferramenta = "matematica",
                    Operacao = "explicar"
                });
            }

            var texto = instrucao.Conteudo.Trim();

            if (texto is "2+2+4" or "10/0" or "10 % 3")
            {
                contexto.Adicionar(
                    "intencao_detectada",
                    "matematica:calcular");

                return Task.FromResult(new DecisaoAgente
                {
                    Acao = TipoAcaoAgente.ExecutarFerramenta,
                    Ferramenta = "matematica",
                    Operacao = "calcular"
                });
            }

            return Task.FromResult(new DecisaoAgente
            {
                Acao = TipoAcaoAgente.Conversar,
                Ferramenta = "conversa",
                Operacao = "responder"
            });
        }
    }

    private sealed class ModeloInteligenciaStub : IModeloInteligencia
    {
        public Task<Resposta> GerarAsync(
            Instrucao instrucao,
            IContexto contexto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new Resposta
                {
                    Conteudo = string.Empty
                });
        }
    }

    private sealed class MemoriaAprendidaStub : IMemoriaAprendida
    {
        public void Aprender(string chave, string valor)
        {
        }

        public void Aprender(
            string chave,
            string valor,
            string tipo,
            int importancia,
            bool explicita,
            string origem,
            IEnumerable<string>? tags)
        {
        }

        public bool Remover(string chave) => false;

        public KAYRON.Core.MemoriaAprendida? Recuperar(string chave) => null;

        public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Pesquisar(
            string consulta,
            int limite = 5)
        {
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();
        }

        public IReadOnlyCollection<KAYRON.Core.MemoriaAprendida> Listar()
        {
            return Array.Empty<KAYRON.Core.MemoriaAprendida>();
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

        public bool Confirmar(
            string id,
            IContexto contexto)
        {
            return true;
        }

        public bool Cancelar(
            string id,
            IContexto contexto)
        {
            return true;
        }

        public SolicitacaoConfirmacao? ObterPendente(
            IContexto contexto)
        {
            return null;
        }
    }
}
