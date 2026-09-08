using System.Globalization;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class GerenciadorConversa
{
    private readonly IHistoricoConversa _historico;
    private readonly IContextoConversacional _contextoConversacional;
    private readonly IResolvedorContexto _resolvedorContexto;
    private readonly IInterpretadorLinguagem _interpretadorLinguagem;
    private readonly IIdentificadorIdioma _identificadorIdioma;
    private readonly IGerenciadorIdioma _gerenciadorIdioma;
    private readonly IDetectorIntencao _detectorIntencao;
    private readonly ILocalizadorResposta _localizadorResposta;
    private readonly IProcessadorConfirmacao _processadorConfirmacao;
    private readonly IOrquestrador _orquestrador;

    public GerenciadorConversa(
        IHistoricoConversa historico,
        IContextoConversacional contextoConversacional,
        IResolvedorContexto resolvedorContexto,
        IInterpretadorLinguagem interpretadorLinguagem,
        IIdentificadorIdioma identificadorIdioma,
        IGerenciadorIdioma gerenciadorIdioma,
        IDetectorIntencao detectorIntencao,
        ILocalizadorResposta localizadorResposta,
        IProcessadorConfirmacao processadorConfirmacao,
        IOrquestrador orquestrador)
    {
        _historico = historico;
        _contextoConversacional = contextoConversacional;
        _resolvedorContexto = resolvedorContexto;
        _interpretadorLinguagem = interpretadorLinguagem;
        _identificadorIdioma = identificadorIdioma;
        _gerenciadorIdioma = gerenciadorIdioma;
        _detectorIntencao = detectorIntencao;
        _localizadorResposta = localizadorResposta;
        _processadorConfirmacao = processadorConfirmacao;
        _orquestrador = orquestrador;
    }

    public async Task<Resposta> ProcessarAsync(
        string entrada,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return new Resposta
            {
                Conteudo = "KAYRON não recebeu uma mensagem."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var mensagemOriginal = entrada.Trim();

        // UMA entrada do usuário = UM único ContextoCiclo.
        var ciclo = new ContextoCiclo(contexto);

        ciclo.Adicionar(
            "entrada_original",
            mensagemOriginal);

        ciclo.Adicionar(
            "mensagem_atual",
            mensagemOriginal);

        // O histórico global é atualizado somente com a entrada atual.
        _historico.AdicionarUsuario(mensagemOriginal);

        // O histórico participa do contexto, mas não substitui a mensagem atual.
        var mensagens = _historico.Listar();

        _contextoConversacional.Atualizar(mensagens);

        ciclo.Adicionar(
            "mensagens_conversa",
            mensagens.Count.ToString());

        ciclo.Adicionar(
            "contexto_conversacional",
            _contextoConversacional.ObterResumo());

        // Resolve referências dependentes da conversa anterior.
        var mensagemResolvida =
            _resolvedorContexto.Resolver(
                mensagemOriginal,
                ciclo);

        ciclo.Adicionar(
            "instrucao_resolvida",
            mensagemResolvida);

        // Identifica o idioma sem gerar resposta.
        var resultadoIdioma =
            _identificadorIdioma.Identificar(
                mensagemResolvida);

        _gerenciadorIdioma.Definir(
            resultadoIdioma.Idioma);

        ciclo.Adicionar(
            "idioma_codigo",
            resultadoIdioma.Idioma.Codigo);

        ciclo.Adicionar(
            "idioma_nome",
            resultadoIdioma.Idioma.Nome);

        ciclo.Adicionar(
            "idioma_confianca",
            resultadoIdioma.Confianca.ToString(
                "F2",
                CultureInfo.InvariantCulture));

        // Interpretação linguística prepara a entrada para o Decisor.
        var mensagemInterpretada =
            _interpretadorLinguagem.Interpretar(
                mensagemResolvida,
                ciclo);

        if (string.IsNullOrWhiteSpace(mensagemInterpretada))
        {
            mensagemInterpretada = mensagemResolvida;
        }

        ciclo.Adicionar(
            "instrucao_interpretada",
            mensagemInterpretada);

        // Detector fornece evidência inicial ao contexto.
        // A decisão final continua pertencendo ao Decisor.
        var intencao =
            _detectorIntencao.Detectar(
                mensagemInterpretada,
                ciclo);

        ciclo.Adicionar(
            "intencao_detectada",
            intencao.Identificada
                ? $"{intencao.Ferramenta}:{intencao.Operacao}"
                : "desconhecida");

        ciclo.Adicionar(
            "intencao_confianca",
            intencao.Confianca.ToString(
                CultureInfo.InvariantCulture));

        ciclo.Adicionar(
            "intencao_evidencia",
            intencao.Evidencia ?? string.Empty);
        // A memória será convocada pelo Decisor.
        // O GerenciadorConversa apenas preserva o contexto da rodada.
        ciclo.Adicionar(
            "memoria:processamento",
            "delegado_ao_decisor");

        var instrucao =
            new Instrucao
            {
                Conteudo = mensagemInterpretada
            };

        // ÚNICO ponto de entrada do ciclo cognitivo.
        var resposta =
            await _orquestrador.ProcessarAsync(
                instrucao,
                ciclo,
                cancellationToken);

        var conteudoLocalizado =
            _localizadorResposta.Localizar(
                resposta.Conteudo,
                resultadoIdioma.Idioma,
                ciclo);

        resposta = new Resposta
        {
            Conteudo = conteudoLocalizado
        };

        ciclo.Adicionar(
            "ultima_resposta",
            resposta.Conteudo);

        if (!string.IsNullOrWhiteSpace(resposta.Conteudo))
        {
            _historico.AdicionarAssistente(
                resposta.Conteudo);
        }

        var mensagensAtualizadas =
            _historico.Listar();

        _contextoConversacional.Atualizar(
            mensagensAtualizadas);

        ciclo.Adicionar(
            "mensagens_conversa",
            mensagensAtualizadas.Count.ToString());

        ciclo.Adicionar(
            "contexto_conversacional",
            _contextoConversacional.ObterResumo());

        // Consolidação somente no final da rodada.
        ciclo.ConsolidarEm(contexto);

        return resposta;
    }
}

