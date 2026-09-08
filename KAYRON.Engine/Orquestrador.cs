using KAYRON.Core;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public sealed class Orquestrador : IOrquestrador
{
    private readonly ExecutorFerramentas _executor;
    private readonly ICatalogoFerramentas _catalogo;
    private readonly IDecisor _decisor;
    private readonly ILogger<Orquestrador> _logger;

    public Orquestrador(
        ExecutorFerramentas executor,
        ICatalogoFerramentas catalogo,
        IDecisor decisor,
        ILogger<Orquestrador> logger)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(logger);

        _executor = executor;
        _catalogo = catalogo;
        _decisor = decisor;
        _logger = logger;
    }

    public async Task<Resposta> ProcessarAsync(
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
            instrucao.Conteudo?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return Finalizar(
                ciclo,
                "KAYRON não recebeu uma instrução.",
                "ENTRADA_VAZIA");
        }

        ciclo.Adicionar(
            "ciclo:entrada_original",
            entrada);

        const int limitePassos = 6;

        for (var passo = 0;
             passo < limitePassos;
             passo++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ciclo.Adicionar(
                "ciclo:passo_atual",
                (passo + 1).ToString());

            var decisao =
                await _decisor.DecidirAsync(
                    instrucao,
                    ciclo,
                    cancellationToken);

            ciclo.Adicionar(
                "ciclo:decisao_atual",
                decisao.Acao.ToString());

            if (decisao.Acao ==
                TipoAcaoAgente.SolicitarInformacao)
            {
                var mensagem =
                    "Preciso de mais informações para executar essa solicitação.";

                ciclo.RegistrarDecisao(
                    "SOLICITAR_INFORMACAO",
                    decisao.Ferramenta,
                    decisao.Operacao,
                    mensagem,
                    true);

                return Finalizar(
                    ciclo,
                    mensagem,
                    "SOLICITAR_INFORMACAO");
            }

            if (decisao.Acao ==
                TipoAcaoAgente.Conversar)
            {
                var resposta =
                    GerarRespostaConversacional(
                        decisao,
                        ciclo);

                ciclo.RegistrarDecisao(
                    "CONVERSAR",
                    decisao.Ferramenta,
                    decisao.Operacao,
                    resposta,
                    true);

                return Finalizar(
                    ciclo,
                    resposta,
                    "CONVERSAR");
            }

            if (decisao.Acao ==
                TipoAcaoAgente.Concluir)
            {
                var resultadoAnterior =
                    ciclo.Obter(
                        "ciclo:ultimo_resultado");

                if (string.IsNullOrWhiteSpace(
                        resultadoAnterior))
                {
                    resultadoAnterior =
                        "Processamento concluído.";
                }

                return Finalizar(
                    ciclo,
                    resultadoAnterior,
                    "CONCLUIR");
            }

            if (decisao.Acao !=
                TipoAcaoAgente.ExecutarFerramenta)
            {
                return Finalizar(
                    ciclo,
                    "KAYRON não encontrou uma ação executável.",
                    "SEM_ACAO");
            }

            if (string.IsNullOrWhiteSpace(
                    decisao.Ferramenta))
            {
                return Finalizar(
                    ciclo,
                    "A decisão não definiu uma ferramenta.",
                    "FERRAMENTA_AUSENTE");
            }

            var ferramenta =
                _catalogo.Obter(
                    decisao.Ferramenta);

            if (ferramenta is null)
            {
                ciclo.RegistrarDecisao(
                    "EXECUTAR",
                    decisao.Ferramenta,
                    decisao.Operacao,
                    "Ferramenta não encontrada.",
                    false);

                return Finalizar(
                    ciclo,
                    $"KAYRON não encontrou a ferramenta '{decisao.Ferramenta}'.",
                    "FERRAMENTA_NAO_ENCONTRADA");
            }

            if (ciclo.AcaoJaExecutada(
                    ferramenta.Nome,
                    decisao.Operacao))
            {
                ciclo.RegistrarDecisao(
                    "BLOQUEAR_REPETICAO",
                    ferramenta.Nome,
                    decisao.Operacao,
                    "Ação já executada neste ciclo.",
                    true);

                return Finalizar(
                    ciclo,
                    $"A ação '{ferramenta.Nome}:{decisao.Operacao}' já foi executada neste ciclo.",
                    "ACAO_JA_EXECUTADA");
            }

            var argumentos =
                decisao.Parametros.TryGetValue(
                    "instrucao",
                    out var argumento)
                    ? argumento
                    : entrada;

            ciclo.Adicionar(
                "ciclo:ferramenta_atual",
                ferramenta.Nome);

            ciclo.Adicionar(
                "ciclo:operacao_atual",
                decisao.Operacao);

            var resultadoFerramenta =
                await _executor.ExecutarAsync(
                    ferramenta.Nome,
                    argumentos,
                    ciclo,
                    cancellationToken,
                    decisao.Operacao);

            var conteudoResultado =
                resultadoFerramenta.Sucesso
                    ? resultadoFerramenta.Conteudo
                    : $"Erro: {resultadoFerramenta.Erro}";

            ciclo.RegistrarDecisao(
                "EXECUTAR",
                ferramenta.Nome,
                decisao.Operacao,
                conteudoResultado,
                resultadoFerramenta.Sucesso);

            ciclo.Adicionar(
                "ciclo:ultimo_resultado",
                conteudoResultado);

            if (!resultadoFerramenta.Sucesso)
            {
                return Finalizar(
                    ciclo,
                    conteudoResultado,
                    "ERRO_EXECUCAO");
            }

            ciclo.Adicionar(
                "ciclo:resultado_disponivel",
                "sim");

            instrucao =
                new Instrucao
                {
                    Conteudo =
                        conteudoResultado
                };

            continue;
        }

        _logger.LogWarning(
            "Limite de decisões atingido para: {Entrada}",
            entrada);

        return Finalizar(
            ciclo,
            "KAYRON interrompeu o ciclo ao atingir o limite de decisões.",
            "LIMITE_CICLO");
    }

    public async Task<Resposta> ProcessarIntencaoAsync(
        Instrucao instrucao,
        IContexto contexto,
        IntencaoDetectada intencao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(intencao);

        var ciclo =
            contexto as ContextoCiclo
            ?? new ContextoCiclo(contexto);

        ciclo.Adicionar(
            "intencao_detectada",
            $"{intencao.Ferramenta}:{intencao.Operacao}");

        ciclo.Adicionar(
            "intencao_confianca",
            intencao.Confianca.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

        ciclo.Adicionar(
            "intencao_evidencia",
            intencao.Evidencia ?? string.Empty);

        return await ProcessarAsync(
            instrucao,
            ciclo,
            cancellationToken);
    }

    private static string GerarRespostaConversacional(
        DecisaoAgente decisao,
        ContextoCiclo ciclo)
    {
        var memoria =
            ciclo.Obter(
                "decisao:memoria_relevante");

        if (decisao.Operacao.Equals(
                "identidade",
                StringComparison.OrdinalIgnoreCase))
        {
            return "KAYRON.";
        }

        if (decisao.Operacao.Equals(
                "capacidades",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Posso conversar, compreender instruções, usar contexto e memória, analisar projetos e código, trabalhar com arquivos, executar ferramentas autorizadas, diagnosticar o sistema, trabalhar com Git e participar de tarefas de desenvolvimento.";
        }

        if (decisao.Operacao.Equals(
                "explicar",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Ação é uma unidade de trabalho decidida pelo Decisor e executada pelo Orquestrador. O ContextoCiclo registra a entrada, as decisões, as ações executadas e seus resultados.";
        }

        if (!string.IsNullOrWhiteSpace(memoria))
        {
            return memoria;
        }

        return "Entendido.";
    }

    private static Resposta Finalizar(
        ContextoCiclo ciclo,
        string conteudo,
        string estado)
    {
        ciclo.Adicionar(
            "ciclo:estado",
            estado);

        ciclo.Adicionar(
            "ciclo:resultado",
            conteudo);

        ciclo.Adicionar(
            "ultima_resposta",
            conteudo);

        return new Resposta
        {
            Conteudo = conteudo
        };
    }
}
