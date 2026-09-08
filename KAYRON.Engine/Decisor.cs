using KAYRON.Core;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public sealed class Decisor : IDecisor
{
    private readonly ConvocadorMemoria _convocadorMemoria;
    private readonly ILogger<Decisor> _logger;

    public Decisor(
        ConvocadorMemoria convocadorMemoria,
        ILogger<Decisor> logger)
    {
        ArgumentNullException.ThrowIfNull(convocadorMemoria);
        ArgumentNullException.ThrowIfNull(logger);

        _convocadorMemoria = convocadorMemoria;
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

        _convocadorMemoria.Convocar(
            entrada,
            ciclo);

        var resultadoDisponivel =
            ciclo.Obter("ciclo:resultado_disponivel");

        var intencao =
            ciclo.Obter("intencao_detectada")
            ?? string.Empty;

        var instrucaoInterpretada =
            ciclo.Obter("instrucao_interpretada")
            ?? entrada;

        _logger.LogInformation(
            "ESTADO DECISOR | Entrada={Entrada} | Intencao={Intencao} | Interpretada={Interpretada} | ResultadoDisponivel={ResultadoDisponivel}",
            entrada,
            intencao,
            instrucaoInterpretada,
            resultadoDisponivel);

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

        var memoria =
            _convocadorMemoria.EncontrarRelevante(
                entrada);

        if (memoria is not null)
        {
            ciclo.Adicionar(
                "decisao:memoria_relevante",
                memoria.Valor);

            return Task.FromResult(
                Registrar(
                    ciclo,
                    new DecisaoAgente
                    {
                        Acao =
                            TipoAcaoAgente.Conversar,
                        Ferramenta = "conversa",
                        Operacao = "memoria",
                        Motivo =
                            "memoria_relevante_disponivel"
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



