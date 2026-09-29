using KAYRON.Core;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public sealed class Decisor : IDecisor
{
    private readonly ILogger<Decisor> _logger;

    public Decisor(
        ConvocadorMemoria convocadorMemoria,
        ILogger<Decisor> logger)
    {
        ArgumentNullException.ThrowIfNull(convocadorMemoria);
        ArgumentNullException.ThrowIfNull(logger);

        _ = convocadorMemoria;
        _logger = logger;
    }

    public Task<DecisaoAgente> DecidirAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        cancellationToken.ThrowIfCancellationRequested();

        var ciclo =
            contexto as ContextoCiclo
            ?? new ContextoCiclo(contexto);

        var entrada =
            ciclo.Obter("mensagem_atual")
            ?? ciclo.Obter("entrada_original")
            ?? instrucao.Conteudo
            ?? string.Empty;

        entrada = entrada.Trim();

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return Task.FromResult(
                Registrar(
                    ciclo,
                    new DecisaoAgente
                    {
                        Acao = TipoAcaoAgente.SolicitarInformacao,
                        Motivo = "entrada_vazia"
                    }));
        }

        // A memória aprendida não decide respostas conversacionais.
        // O conhecimento persistido será usado somente como fallback quando a IA estiver indisponível.

        var resultadoDisponivel =
            ciclo.Obter("ciclo:resultado_disponivel");

        var intencao =
            ciclo.Obter("intencao_detectada")
            ?? string.Empty;

        var instrucaoInterpretada =
            ciclo.Obter("instrucao_interpretada")
            ?? entrada;

        var continuacaoOrigemInternet =
            ciclo.Obter("continuacao_origem_internet");

        if (string.Equals(
                continuacaoOrigemInternet,
                "sim",
                StringComparison.OrdinalIgnoreCase) &&
            !EhIntencaoTecnica(intencao))
        {
            ciclo.Adicionar(
                "intencao_detectada",
                "internet:pesquisar");

            ciclo.Adicionar(
                "intencao_evidencia",
                "continuação de conversa originada por pesquisa na Internet");

            intencao = "internet:pesquisar";
        }

        _logger.LogInformation(
            "ESTADO DECISOR | Entrada={Entrada} | Intencao={Intencao} | Interpretada={Interpretada} | ResultadoDisponivel={ResultadoDisponivel}",
            entrada,
            intencao,
            instrucaoInterpretada,
            resultadoDisponivel);

        if (intencao.Equals(
                "internet:pesquisar",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                resultadoDisponivel,
                "sim",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                ciclo.Obter("ultima_origem_ferramenta"),
                "internet",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                ciclo.Obter("ultima_origem_operacao"),
                "pesquisar",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                Registrar(
                    ciclo,
                    new DecisaoAgente
                    {
                        Acao = TipoAcaoAgente.Concluir,
                        Ferramenta = "internet",
                        Operacao = "pesquisar",
                        Motivo = "resultado_da_pesquisa_disponivel"
                    }));
        }

        if (intencao.Equals(
                "internet:pesquisar",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                Registrar(
                    ciclo,
                    new DecisaoAgente
                    {
                        Acao = TipoAcaoAgente.ExecutarFerramenta,
                        Ferramenta = "internet",
                        Operacao = "pesquisar",
                        Motivo = "pesquisa_externa_solicitada",
                        Parametros = new Dictionary<string, string>
                        {
                            ["instrucao"] = instrucaoInterpretada
                        }
                    }));
        }

        if (EhIntencaoTecnica(intencao))
        {
            var partes =
                intencao.Split(
                    ':',
                    2,
                    StringSplitOptions.RemoveEmptyEntries);

            var ferramenta =
                partes.Length > 0
                    ? partes[0].Trim()
                    : string.Empty;

            var operacao =
                partes.Length > 1
                    ? partes[1].Trim()
                    : string.Empty;

            ciclo.Adicionar(
                "intencao_detectada",
                string.Empty);

            return Task.FromResult(
                Registrar(
                    ciclo,
                    new DecisaoAgente
                    {
                        Acao =
                            TipoAcaoAgente.ExecutarFerramenta,
                        Ferramenta = ferramenta,
                        Operacao = operacao,
                        Motivo =
                            "intencao_detectada_no_contexto",
                        Parametros =
                            new Dictionary<string, string>
                            {
                                ["instrucao"] =
                                    instrucaoInterpretada
                            }
                    }));
        }

        if (EhConversa(intencao))
        {
            return Task.FromResult(
                Registrar(
                    ciclo,
                    new DecisaoAgente
                    {
                        Acao =
                            TipoAcaoAgente.Conversar,
                        Ferramenta = "conversa",
                        Operacao =
                            ExtrairOperacao(intencao),
                        Motivo =
                            "intencao_conversacional"
                    }));
        }

        _logger.LogInformation(
            "Decisor tratando entrada natural como conversa: {Entrada}",
            entrada);

        return Task.FromResult(
            Registrar(
                ciclo,
                new DecisaoAgente
                {
                    Acao = TipoAcaoAgente.Conversar,
                    Ferramenta = "conversa",
                    Operacao = "responder",
                    Motivo =
                        "entrada_natural_sem_intencao_identificada"
                }));
    }

    private static DecisaoAgente Registrar(
        ContextoCiclo ciclo,
        DecisaoAgente decisao)
    {
        ciclo.Adicionar(
            "decisao:tipo",
            decisao.Acao.ToString());

        ciclo.Adicionar(
            "decisao:acao",
            string.IsNullOrWhiteSpace(
                decisao.Ferramenta)
                ? decisao.Acao.ToString()
                : $"{decisao.Ferramenta}:{decisao.Operacao}");

        ciclo.Adicionar(
            "decisao:motivo",
            decisao.Motivo);

        return decisao;
    }

    private static bool EhIntencaoTecnica(
        string intencao)
    {
        if (string.IsNullOrWhiteSpace(intencao))
            return false;

        if (intencao.Equals(
                "desconhecida",
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (intencao.StartsWith(
                "conversa:",
                StringComparison.OrdinalIgnoreCase))
            return false;

        if (intencao.Equals(
                "internet:pesquisar",
                StringComparison.OrdinalIgnoreCase))
            return false;

        return intencao.Contains(':');
    }

    private static bool EhConversa(
        string intencao)
    {
        return intencao.StartsWith(
            "conversa:",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtrairOperacao(
        string intencao)
    {
        var partes =
            intencao.Split(
                ':',
                2,
                StringSplitOptions.RemoveEmptyEntries);

        return partes.Length > 1
            ? partes[1]
            : "responder";
    }
}
