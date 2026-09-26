using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using KAYRON.Core;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public sealed class Orquestrador : IOrquestrador
{
    private readonly ExecutorFerramentas _executor;
    private readonly ICatalogoFerramentas _catalogo;
    private readonly IDecisor _decisor;
    private readonly IModeloInteligencia _modeloInteligencia;
    private readonly GestorConhecimento _gestorConhecimento;
    private readonly IMemoriaAprendida _memoriaAprendida;
    private readonly ILogger<Orquestrador> _logger;

    public Orquestrador(
        ExecutorFerramentas executor,
        ICatalogoFerramentas catalogo,
        IDecisor decisor,
        IModeloInteligencia modeloInteligencia,
        GestorConhecimento gestorConhecimento,
        IMemoriaAprendida memoriaAprendida,
        ILogger<Orquestrador> logger)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(gestorConhecimento);
        ArgumentNullException.ThrowIfNull(memoriaAprendida);

        _memoriaAprendida = memoriaAprendida;

        _executor = executor;
        _catalogo = catalogo;
        _decisor = decisor;
        _modeloInteligencia = modeloInteligencia;
        _gestorConhecimento = gestorConhecimento;
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

        if (EhSaudacao(entrada))
        {
            return Finalizar(
                ciclo,
                "Olá! Como posso ajudar?",
                "SAUDACAO");
        }

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return Finalizar(
                ciclo,
                "KAYRON nÃ£o recebeu uma instruÃ§Ã£o.",
                "ENTRADA_VAZIA");
        }

        ciclo.Adicionar(
            "ciclo:entrada_original",
            entrada);

        ciclo.Adicionar(
            "ciclo:resultado_disponivel",
            string.Empty);

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

            if (string.Equals(decisao.Ferramenta, "matematica", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(decisao.Operacao, "calcular", StringComparison.OrdinalIgnoreCase))
            {
                var resultadoMatematico = CalcularExpressao(entrada);

                if (resultadoMatematico is null)
                {
                    return Finalizar(
                        ciclo,
                        "Não consegui interpretar a expressão matemática.",
                        "ERRO_MATEMATICA");
                }

                ciclo.RegistrarDecisao(
                    "CALCULAR",
                    "matematica",
                    "calcular",
                    resultadoMatematico,
                    true);

                return Finalizar(
                    ciclo,
                    $"O resultado é {resultadoMatematico}.",
                    "MATEMATICA");
            }

            if (decisao.Acao ==
                TipoAcaoAgente.SolicitarInformacao)
            {
                var mensagem =
                    "Preciso de mais informaÃ§Ãµes para executar essa solicitaÃ§Ã£o.";

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
                var intencaoAntesDaPesquisa =
                    ciclo.Obter("intencao_detectada");

                if (string.Equals(
                        decisao.Operacao,
                        "responder",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await _gestorConhecimento.AdquirirSeNecessarioAsync(
                        instrucao,
                        ciclo,
                        cancellationToken);
                }

                var conhecimento =
                    ciclo.Obter("conhecimento:resposta");

                if (!string.IsNullOrWhiteSpace(conhecimento))
            {
                if (string.Equals(
                        ciclo.Obter("correcao_resposta:ativa"),
                        "sim",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ciclo.Adicionar(
                        "correcao_resposta:evidencia_verificada",
                        conhecimento);

                    var respostaCorrigida =
                        await _modeloInteligencia.GerarAsync(
                            instrucao,
                            ciclo,
                            cancellationToken);

                    var conteudoCorrigido =
                        respostaCorrigida.Conteudo.Trim();

                    var chaveMemoria =
                        ciclo.Obter("conhecimento:chave");

                    if (!string.IsNullOrWhiteSpace(chaveMemoria) &&
                        !string.IsNullOrWhiteSpace(conteudoCorrigido) &&
                        EhRespostaCorrigidaConfiavel(conteudoCorrigido))
                    {
                        _memoriaAprendida.Aprender(
                            chaveMemoria,
                            conteudoCorrigido,
                            "conhecimento",
                            5,
                            false,
                            "correcao_verificada",
                            new[] { "conhecimento", "correcao", "verificado" });
                    }

                    ciclo.RegistrarDecisao(
                        "CORRECAO_VERIFICADA",
                        "internet",
                        "pesquisar",
                        conteudoCorrigido,
                        true);

                    return Finalizar(
                        ciclo,
                        conteudoCorrigido,
                        "CORRECAO_VERIFICADA");
                }

                ciclo.RegistrarDecisao(
                    "CONHECIMENTO",
                    decisao.Ferramenta,
                    decisao.Operacao,
                    conhecimento,
                    true);

                return Finalizar(
                    ciclo,
                    conhecimento,
                    "CONHECIMENTO");
            }

                if (string.Equals(
                        decisao.Operacao,
                        "responder",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        ciclo.Obter("conhecimento:estado"),
                        "nao_adquirido",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ciclo.Adicionar(
                        "conhecimento:estado",
                        "nao_disponivel");
                }

                if (!string.Equals(
                        intencaoAntesDaPesquisa,
                        "internet:pesquisar",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ciclo.Adicionar(
                        "intencao_detectada",
                        "conversa:responder");
                }

                var resposta =
                    await _modeloInteligencia.GerarAsync(
                        instrucao,
                        ciclo,
                        cancellationToken);

                ciclo.RegistrarDecisao(
                    "CONVERSAR",
                    decisao.Ferramenta,
                    decisao.Operacao,
                    resposta.Conteudo,
                    true);

                return Finalizar(
                    ciclo,
                    resposta.Conteudo,
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
                        "Processamento concluÃ­do.";
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
                    "KAYRON nÃ£o encontrou uma aÃ§Ã£o executÃ¡vel.",
                    "SEM_ACAO");
            }

            if (string.IsNullOrWhiteSpace(
                    decisao.Ferramenta))
            {
                return Finalizar(
                    ciclo,
                    "A decisÃ£o nÃ£o definiu uma ferramenta.",
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
                    "Ferramenta nÃ£o encontrada.",
                    false);

                return Finalizar(
                    ciclo,
                    $"KAYRON nÃ£o encontrou a ferramenta '{decisao.Ferramenta}'.",
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
                    "AÃ§Ã£o jÃ¡ executada neste ciclo.",
                    true);

                return Finalizar(
                    ciclo,
                    $"A aÃ§Ã£o '{ferramenta.Nome}:{decisao.Operacao}' jÃ¡ foi executada neste ciclo.",
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

            ciclo.Adicionar(
                "ultima_origem_ferramenta",
                ferramenta.Nome);

            ciclo.Adicionar(
                "ultima_origem_operacao",
                decisao.Operacao);

            ciclo.Adicionar(
                "ultima_origem_resultado",
                conteudoResultado);
if (string.Equals(
                    ciclo.Obter("correcao_resposta:ativa"),
                    "sim",
                    StringComparison.OrdinalIgnoreCase))
            {
                var perguntaOriginal =
                    ciclo.Obter("correcao_resposta:pergunta_original")
                    ?? entrada;

                var evidenciaParaModelo =
                    PrepararEvidenciaParaCorrecao(
                        conteudoResultado,
                        perguntaOriginal);

                ciclo.Adicionar(
                    "correcao_resposta:evidencia_verificada",
                    evidenciaParaModelo);
                ciclo.Adicionar(
                    "conhecimento:adquirido",
                    evidenciaParaModelo);
                ciclo.Adicionar(
                    "ciclo:ultimo_resultado",
                    evidenciaParaModelo);

                var respostaCorrigida =
                    await _modeloInteligencia.GerarAsync(
                        new Instrucao { Conteudo = perguntaOriginal },
                        ciclo,
                        cancellationToken);

                var conteudoCorrigido =
                    respostaCorrigida.Conteudo.Trim();

                var chaveMemoria =
                    ciclo.Obter("conhecimento:chave");

                if (string.IsNullOrWhiteSpace(chaveMemoria))
                {
                    var bytesChave =
                        SHA256.HashData(
                            Encoding.UTF8.GetBytes(
                                perguntaOriginal.Trim().ToLowerInvariant()));

                    chaveMemoria =
                        "conhecimento_" +
                        Convert.ToHexString(bytesChave)[..24].ToLowerInvariant();
                }

                if (!string.IsNullOrWhiteSpace(chaveMemoria) &&
                    !string.IsNullOrWhiteSpace(conteudoCorrigido) &&
                        EhRespostaCorrigidaConfiavel(conteudoCorrigido))
                {
                    _memoriaAprendida.Aprender(
                        chaveMemoria,
                        conteudoCorrigido,
                        "conhecimento",
                        5,
                        false,
                        "correcao_verificada",
                        new[] { "conhecimento", "correcao", "verificado" });
                }

                ciclo.RegistrarDecisao(
                    "CORRECAO_VERIFICADA",
                    "internet",
                    "pesquisar",
                    conteudoCorrigido,
                    true);

                return Finalizar(
                    ciclo,
                    conteudoCorrigido,
                    "CORRECAO_VERIFICADA");
            }

            instrucao =
                new Instrucao
                {
                    Conteudo =
                        conteudoResultado
                };

            continue;
        }

        _logger.LogWarning(
            "Limite de decisÃµes atingido para: {Entrada}",
            entrada);

        return Finalizar(
            ciclo,
            "KAYRON interrompeu o ciclo ao atingir o limite de decisÃµes.",
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
            return "Posso conversar, compreender instruÃ§Ãµes, usar contexto e memÃ³ria, analisar projetos e cÃ³digo, trabalhar com arquivos, executar ferramentas autorizadas, diagnosticar o sistema, trabalhar com Git e participar de tarefas de desenvolvimento.";
        }

        if (decisao.Operacao.Equals(
                "explicar",
                StringComparison.OrdinalIgnoreCase))
        {
            return "AÃ§Ã£o Ã© uma unidade de trabalho decidida pelo Decisor e executada pelo Orquestrador. O ContextoCiclo registra a entrada, as decisÃµes, as aÃ§Ãµes executadas e seus resultados.";
        }

        if (!string.IsNullOrWhiteSpace(memoria))
        {
            return memoria;
        }

        return "Entendido.";
    }

    private static string PrepararEvidenciaParaCorrecao(
        string evidencia,
        string pergunta)
    {
        if (string.IsNullOrWhiteSpace(evidencia) || evidencia.Length <= 12000)
            return evidencia;

        var termos = pergunta
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 5)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var trechos = new List<string>();
        trechos.Add(evidencia[..Math.Min(1500, evidencia.Length)]);

        foreach (var termo in termos)
        {
            foreach (Match match in Regex.Matches(
                         evidencia,
                         Regex.Escape(termo),
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var inicioTrecho = Math.Max(0, match.Index - 1800);
                var tamanhoTrecho = Math.Min(3600, evidencia.Length - inicioTrecho);
                trechos.Add(evidencia.Substring(inicioTrecho, tamanhoTrecho));

                if (trechos.Count >= 6)
                    break;
            }

            if (trechos.Count >= 6)
                break;
        }

        var resultado = string.Join(
            Environment.NewLine + Environment.NewLine + "--- EVIDÊNCIA ---" + Environment.NewLine,
            trechos.Distinct(StringComparer.Ordinal));

        return resultado.Length > 12000
            ? resultado[..12000]
            : resultado;
    }

    private static bool EhRespostaCorrigidaConfiavel(string resposta)
    {
        var texto = resposta.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var marcadoresDeNaoConfirmacao = new[]
        {
            "não disponho de base suficiente",
            "nao disponho de base suficiente",
            "não foi possível confirmar",
            "nao foi possivel confirmar",
            "não consegui confirmar",
            "nao consegui confirmar",
            "evidência recuperada na pesquisa não contém",
            "evidencia recuperada na pesquisa nao contem",
            "não há base suficiente",
            "nao ha base suficiente"
        };

        if (marcadoresDeNaoConfirmacao.Any(
                marcador => texto.Contains(marcador, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !texto.StartsWith(
            "Pesquisa na Internet:",
            StringComparison.OrdinalIgnoreCase) &&
               !texto.StartsWith(
                   "Fonte: pesquisa web",
                   StringComparison.OrdinalIgnoreCase);
    }
    private static string? CalcularExpressao(string entrada)
    {
        var match = Regex.Match(
            entrada,
            @"(?<![A-Za-z0-9_])(\d+(?:[.,]\d+)?)\s*([+\-*/x×÷])\s*(\d+(?:[.,]\d+)?)(?![A-Za-z0-9_])",
            RegexOptions.CultureInvariant);

        if (!match.Success)
            return null;

        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        if (!decimal.TryParse(match.Groups[1].Value.Replace('.', ','), NumberStyles.Number, cultura, out var esquerda) ||
            !decimal.TryParse(match.Groups[3].Value.Replace('.', ','), NumberStyles.Number, cultura, out var direita))
            return null;

        var operador = match.Groups[2].Value;
        decimal resultado;

        switch (operador)
        {
            case "+": resultado = esquerda + direita; break;
            case "-": resultado = esquerda - direita; break;
            case "*":
            case "x":
            case "×": resultado = esquerda * direita; break;
            case "/":
            case "÷":
                if (direita == 0) return "Erro: divisão por zero.";
                resultado = esquerda / direita;
                break;
            default: return null;
        }

        return resultado.ToString("0.############################", cultura);
    }

    private static bool EhSaudacao(string entrada)
    {
        var texto = entrada
            .Trim()
            .TrimEnd('.', '!', '?', ',', ';', ':')
            .ToLowerInvariant();

        return texto is "ola" or "olá" or "oi" or "bom dia" or "boa tarde" or "boa noite";
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
