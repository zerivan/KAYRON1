using System.Text;
using System.Text.RegularExpressions;
using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public sealed class DeepSearch : IModeloInteligencia
{
    private readonly PesquisaWeb _web;
    private readonly ModeloInteligenciaGemini _modelo;
    private readonly ILogger<DeepSearch> _logger;
    private readonly IEmbeddingMemoria _embedding;

    public DeepSearch(
        HttpClient http,
        PesquisaWeb web,
        IEmbeddingMemoria embedding,
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<DeepSearch> logger,
        ILogger<ModeloInteligenciaHuggingFace> modeloLogger)
    {
        _web = web;
        _embedding = embedding;
        _modelo = new ModeloInteligenciaGemini(http, options, NullLogger<ModeloInteligenciaGemini>.Instance);
        _logger = logger;
    }

    public Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        var intencao = contexto.Obter("intencao_detectada");
        if (string.Equals(intencao, "internet:pesquisar", StringComparison.OrdinalIgnoreCase))
            return ExecutarPesquisaAsync(instrucao, contexto, cancellationToken);

        return _modelo.GerarAsync(instrucao, contexto, cancellationToken);
    }

    private async Task<Resposta> ExecutarPesquisaAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken)
    {
        var consulta = NormalizarConsulta(instrucao.Conteudo);
        var fontes = new List<ResultadoPesquisaWeb>();
        var termosConsulta = ExtrairTermos(consulta);
        if (EhConsultaPuramenteConversa(consulta))
            return new Resposta { Conteudo = string.Empty };

        if (termosConsulta.Length == 0)
            return new Resposta { Conteudo = string.Empty };

        var swEmbedding = System.Diagnostics.Stopwatch.StartNew();
        var vetorConsulta = await _embedding.GerarAsync(consulta, cancellationToken);
        swEmbedding.Stop();
        _logger.LogInformation("DEEPSEARCH: embedding_consulta_ms={Ms}", swEmbedding.ElapsedMilliseconds);
        var vetoresFontes = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        var consultaBusca = ConstruirConsultaBusca(consulta, termosConsulta);
        var solicitacaoFonte = string.Equals(
            contexto.Obter("fonte_solicitada"),
            "sim",
            StringComparison.OrdinalIgnoreCase);
        var respostaOriginal = contexto.Obter("fonte_resposta_original");

        if (solicitacaoFonte && !string.IsNullOrWhiteSpace(respostaOriginal))
        {
            var termosRespostaOriginal = ExtrairTermosFatuais(respostaOriginal)
                .Take(8)
                .ToArray();

            if (termosRespostaOriginal.Length > 0)
            {
                consultaBusca =
                    $"{consultaBusca} {string.Join(' ', termosRespostaOriginal)}";
            }
        }

        await Buscar(consultaBusca, fontes, cancellationToken);

        if (solicitacaoFonte && !string.IsNullOrWhiteSpace(respostaOriginal))
        {
            var consultaWikipedia =
                $"{consulta} " +
                $"{string.Join(' ', ExtrairTermosFatuais(respostaOriginal).Take(6))}";

            var fontesWikipedia = await _web.PesquisarAsync(
                consultaWikipedia,
                8,
                cancellationToken);

            foreach (var fonteWikipedia in fontesWikipedia)
            {
                if (!fontes.Any(x =>
                        x.Url.Equals(
                            fonteWikipedia.Url,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    fontes.Add(fonteWikipedia);
                }
            }
        }
        _logger.LogInformation("DEEPSEARCH: consulta={Consulta}; fontes_brutas={Quantidade}", consulta, fontes.Count);
        await RemoverFontesIrrelevantesAsync(consulta, fontes, vetorConsulta, vetoresFontes, cancellationToken);
        _logger.LogInformation("DEEPSEARCH: fontes_apos_filtro={Quantidade}", fontes.Count);
        if (fontes.Count > 0)
            await OrdenarFontesPorSemanticaAsync(fontes, vetorConsulta, vetoresFontes, cancellationToken);
        if (fontes.Count == 0 && termosConsulta.Length > 0)
        {
            await Buscar(string.Join(' ', termosConsulta) + " site:gov.br", fontes, cancellationToken);
            await RemoverFontesIrrelevantesAsync(consulta, fontes, vetorConsulta, vetoresFontes, cancellationToken);
        }

        if (fontes.Count == 0 && termosConsulta.Length > 0)
        {
            await Buscar(string.Join(' ', termosConsulta) + " documentação oficial", fontes, cancellationToken);
            await RemoverFontesIrrelevantesAsync(consulta, fontes, vetorConsulta, vetoresFontes, cancellationToken);
        }
        if (fontes.Count == 0 && termosConsulta.Length > 0)
        {
            await Buscar(string.Join(' ', termosConsulta) + " .NET C#", fontes, cancellationToken);
            await RemoverFontesIrrelevantesAsync(consulta, fontes, vetorConsulta, vetoresFontes, cancellationToken);
        }

        if (fontes.Count == 0)
            return new Resposta { Conteudo = string.Empty };

        var contextoPesquisa = new Contexto();
        contextoPesquisa.Adicionar("entrada_original", consulta);
        contextoPesquisa.Adicionar("intencao_detectada", "internet:pesquisar");

        await OrdenarFontesPorSemanticaAsync(fontes, vetorConsulta, vetoresFontes, cancellationToken);
        if (fontes.Count == 0)
            return new Resposta { Conteudo = string.Empty };

        var fontesRespostaInicial = fontes.ToList();

        var prompt = new StringBuilder();
        prompt.AppendLine("MODO: ANÁLISE DE PESQUISA DO KAYRON.");
        prompt.AppendLine("O agente KAYRON já filtrou as fontes. Você é apenas o sintetizador dos dados recuperados.");
        prompt.AppendLine("A pesquisa web já foi executada. Analise exclusivamente os dados recuperados e devolva uma síntese factual para o KAYRON.");

        if (solicitacaoFonte)
        {
            prompt.AppendLine("O usuário está pedindo a fonte da informação apresentada anteriormente.");
            prompt.AppendLine("Não trate a resposta anterior como uma fonte. Verifique a afirmação contra as fontes recuperadas nesta nova pesquisa.");
            prompt.AppendLine("Informe de forma objetiva quais fontes sustentam a informação anterior. Se as fontes não a sustentarem, diga explicitamente que a informação anterior não foi confirmada.");
            prompt.AppendLine("NÃO invente autoria, documento, site ou origem. Use somente as fontes recuperadas.");
            prompt.AppendLine("Quando uma fonte sustentar uma afirmação, marque-a no texto com [FONTE N], usando exatamente o número da fonte apresentada abaixo.");
            prompt.AppendLine("Não use números de fonte que não existam na lista.");
            prompt.AppendLine();
            prompt.AppendLine("RESPOSTA ANTERIOR A SER VERIFICADA:");
            prompt.AppendLine(respostaOriginal ?? "não disponível");
            prompt.AppendLine();
        }
        prompt.AppendLine("Use SOMENTE as informações sustentadas pelos trechos das fontes abaixo.");
        prompt.AppendLine("NÃO use conhecimento geral, memória do modelo, histórico da conversa ou conhecimento prévio.");
        prompt.AppendLine("NÃO diga que já possui a informação em uma base de conhecimento.");
        prompt.AppendLine("NÃO diga que a pesquisa não foi realizada e NÃO ofereça realizar outra pesquisa.");
        prompt.AppendLine("Não responda sobre assuntos que não estejam sustentados pelas fontes recuperadas.");
        prompt.AppendLine("Responda diretamente ao que foi perguntado, respeitando o assunto, as categorias, os critérios, as unidades e o período mencionados pelo usuário.");
        prompt.AppendLine("Não confunda categorias relacionadas nem trate uma menção ao tema como evidência suficiente para responder.");
        prompt.AppendLine("Se as fontes não sustentarem uma resposta factual suficiente, informe exatamente isso.");
        prompt.AppendLine("Produza somente a síntese factual necessária para o KAYRON; não converse diretamente com o usuário.");
        prompt.AppendLine("Para rastreabilidade, marque as afirmações sustentadas com [FONTE N], usando apenas números existentes na lista FONTES.");
        prompt.AppendLine("Integre as informações em uma síntese natural, clara e concisa. Evite listas longas quando um parágrafo resolver. Não force fontes irrelevantes.");
        prompt.AppendLine("Não insira URLs nem links no corpo do texto. Os marcadores [FONTE N] serão removidos e as fontes efetivamente citadas serão reunidas no final da resposta.");
        prompt.AppendLine();
        prompt.AppendLine("PERGUNTA:");
        prompt.AppendLine(consulta);
        prompt.AppendLine();
        prompt.AppendLine("FONTES:");
        prompt.AppendLine(Fontes(fontes));
        _logger.LogInformation("DEEPSEARCH GEMINI_ENTRADA: consulta={Consulta}; fontes={Fontes}; prompt={Prompt}", consulta, fontes.Count, prompt.ToString());

        var contextoAnalise = new Contexto();
        contextoAnalise.Adicionar("entrada_original", consulta);
        contextoAnalise.Adicionar("intencao_detectada", "internet:pesquisar");

        Resposta? respostaInicial = null;

        try
        {
            var resposta = await ModeloAuxiliar(prompt.ToString(), contextoAnalise, cancellationToken);
            respostaInicial = resposta;
            _logger.LogInformation("DEEPSEARCH GEMINI_SAIDA: consulta={Consulta}; resposta={Resposta}", consulta, resposta.Conteudo);
            _logger.LogInformation("DEEPSEARCH: síntese inicial; resposta_valida={Valida}; evidencia_insuficiente={Insuficiente}; caracteres={Caracteres}", EhRespostaPesquisaValida(resposta.Conteudo), EhEvidenciaInsuficiente(resposta.Conteudo), resposta.Conteudo?.Length ?? 0);
            if (EhRespostaPesquisaValida(resposta.Conteudo) &&
                !EhEvidenciaInsuficiente(resposta.Conteudo))
            {
                _logger.LogInformation("DEEPSEARCH: entrando em AnexarFontes; consulta={Consulta}; fontes_retornadas={Quantidade}", consulta, fontes.Count);
                return AnexarFontes(resposta, consulta, fontes, solicitacaoFonte);
            }

            // Se a primeira pesquisa não sustentar a resposta, não encerre o turno.
            // Refaz a busca com consultas de contexto antes de admitir falta de evidência.
            // Preserve a expressão principal da pergunta nas tentativas alternativas.
            // Não remova palavras funcionais aqui: isso pode mudar o conceito pesquisado.
            var consultaAlternativaBase = ConstruirConsultaBusca(consulta, termosConsulta);
            foreach (var consultaAlternativa in new[]
                     {
                         $"{consultaAlternativaBase} fontes oficiais",
                         $"{consultaAlternativaBase} dados e referências confiáveis"
                     })
            {
                fontes.Clear();
                await Buscar(consultaAlternativa, fontes, cancellationToken);
                await RemoverFontesIrrelevantesAsync(consulta, fontes, vetorConsulta, vetoresFontes, cancellationToken);

                if (fontes.Count == 0)
                    continue;

                await OrdenarFontesPorSemanticaAsync(fontes, vetorConsulta, vetoresFontes, cancellationToken);

                prompt = new StringBuilder();
                prompt.AppendLine("MODO: ANÁLISE DE PESQUISA DO KAYRON.");
                prompt.AppendLine("A primeira pesquisa não forneceu evidência suficiente. Esta é uma nova tentativa de pesquisa.");

                if (solicitacaoFonte)
                {
                    prompt.AppendLine("O usuário está pedindo a fonte da informação apresentada anteriormente.");
                    prompt.AppendLine("Verifique a resposta anterior exclusivamente contra as novas fontes recuperadas.");
                    prompt.AppendLine("Informe objetivamente quais fontes sustentam a informação ou diga que ela não foi confirmada.");
                    prompt.AppendLine("NÃO invente autoria, documento, site ou origem.");
                    prompt.AppendLine("Quando uma fonte sustentar uma afirmação, marque-a no texto com [FONTE N], usando exatamente o número da fonte apresentada abaixo.");
                    prompt.AppendLine("Não use números de fonte que não existam na lista.");
                    prompt.AppendLine();
                    prompt.AppendLine("RESPOSTA ANTERIOR A SER VERIFICADA:");
                    prompt.AppendLine(respostaOriginal ?? "não disponível");
                    prompt.AppendLine();
                }

                prompt.AppendLine("Use SOMENTE as informações sustentadas pelos trechos das fontes abaixo.");
                prompt.AppendLine("Não use memória, conhecimento prévio ou suposições.");
                prompt.AppendLine("Responda diretamente ao que foi perguntado, respeitando o assunto, as categorias, os critérios, as unidades e o período mencionados pelo usuário.");
                prompt.AppendLine("Se ainda houver lacunas, faça a melhor síntese possível apenas com o que estiver sustentado pelas fontes.");
                prompt.AppendLine("Para rastreabilidade, marque as afirmações sustentadas com [FONTE N], usando apenas números existentes na lista FONTES.");
        prompt.AppendLine("Integre as informações em uma síntese natural, clara e concisa. Evite listas longas quando um parágrafo resolver. Não force fontes irrelevantes.");
        prompt.AppendLine("Não insira URLs nem links no corpo do texto. Os marcadores [FONTE N] serão removidos e as fontes efetivamente citadas serão reunidas no final da resposta.");
                prompt.AppendLine();
                prompt.AppendLine("PERGUNTA:");
                prompt.AppendLine(consulta);
                prompt.AppendLine();
                prompt.AppendLine("FONTES:");
                prompt.AppendLine(Fontes(fontes));

                var novaResposta = await ModeloAuxiliar(prompt.ToString(), contextoAnalise, cancellationToken);
                if (EhRespostaPesquisaValida(novaResposta.Conteudo) &&
                    !EhEvidenciaInsuficiente(novaResposta.Conteudo))
                {
                    _logger.LogInformation("DEEPSEARCH: entrando em AnexarFontes após nova busca; consulta={Consulta}; fontes_retornadas={Quantidade}", consulta, fontes.Count);
                    return AnexarFontes(novaResposta, consulta, fontes, solicitacaoFonte);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Síntese Gemini indisponível; usando somente conteúdo recuperado.");
        }

        if (respostaInicial is not null &&
            !string.IsNullOrWhiteSpace(respostaInicial.Conteudo) &&
            EhRespostaPesquisaValida(respostaInicial.Conteudo) &&
            !EhEvidenciaInsuficiente(respostaInicial.Conteudo))
        {
            _logger.LogInformation("DEEPSEARCH: entrando em AnexarFontes com resposta inicial; consulta={Consulta}; fontes_retornadas={Quantidade}", consulta, fontesRespostaInicial.Count);
            return AnexarFontes(respostaInicial, consulta, fontesRespostaInicial, solicitacaoFonte);
        }

        _logger.LogWarning("DEEPSEARCH: síntese descartada; consulta={Consulta}; resposta_inicial_valida={Valida}; evidencia_insuficiente={Insuficiente}; fontes_iniciais={Quantidade}", consulta, respostaInicial is not null && EhRespostaPesquisaValida(respostaInicial.Conteudo), respostaInicial is not null && EhEvidenciaInsuficiente(respostaInicial.Conteudo), fontesRespostaInicial.Count);
        // Uma síntese insuficiente não é uma resposta factual.
        // Não devolva ao Orquestrador texto que possa ser interpretado como evidência.
        return new Resposta { Conteudo = string.Empty };
    }

    private async Task RemoverFontesIrrelevantesAsync(
        string consulta,
        List<ResultadoPesquisaWeb> fontes,
        float[]? vetorConsulta,
        Dictionary<string, float[]> vetoresFontes,
        CancellationToken cancellationToken)
    {
        var termos = ExtrairTermos(consulta);
        if (termos.Length == 0)
            return;

        var avaliadas = new List<(ResultadoPesquisaWeb Fonte, int Pontos)>();
        foreach (var fonte in fontes)
        {
            var pontos = await CalcularPontuacaoFonteAsync(
                fonte,
                termos,
                vetorConsulta,
                vetoresFontes,
                cancellationToken);
            avaliadas.Add((fonte, pontos));
        }

        // Calcule também similaridade semântica e permita manter fontes cuja
        // pontuação sintática seja baixa, mas a similaridade semântica com a
        // consulta seja suficientemente alta. Isso reduz falsos negativos
        // quando as fontes usam sinônimos ou estrutura diferente da consulta.
        var thresholdSemanticaAceitavel = 0.60; // configurável

        var avaliadasComSemantica = new List<(ResultadoPesquisaWeb Fonte, int Pontos, double Semantica)>();
        foreach (var a in avaliadas)
        {
            double sem = 0d;
            if (vetorConsulta is not null)
            {
                try
                {
                    if (!vetoresFontes.TryGetValue(a.Fonte.Url, out var vf))
                    {
                        var texto = $"{a.Fonte.Titulo} {a.Fonte.Trecho} {a.Fonte.Url}";
                        vf = await _embedding.GerarAsync(texto, cancellationToken).ConfigureAwait(false);
                        vetoresFontes[a.Fonte.Url] = vf;
                    }

                    sem = Cosine(vetorConsulta, vf);
                }
                catch
                {
                    sem = 0d;
                }
            }

            avaliadasComSemantica.Add((a.Fonte, a.Pontos, sem));
        }

        foreach (var avaliada in avaliadasComSemantica.OrderByDescending(x => x.Pontos).Take(12))
            _logger.LogInformation("DEEPSEARCH FONTE: pontos={Pontos}; semantica={Semantica:F2}; titulo={Titulo}; url={Url}", avaliada.Pontos, avaliada.Semantica, avaliada.Fonte.Titulo, avaliada.Fonte.Url);

        var filtradas = avaliadasComSemantica
            .Where(x => x.Pontos >= 2 || x.Semantica >= thresholdSemanticaAceitavel)
            .OrderByDescending(x => x.Pontos)
            .ThenByDescending(x => x.Semantica)
            .ThenByDescending(x => EhFonteConfiavel(x.Fonte.Url))
            .Take(7)
            .Select(x => x.Fonte)
            .ToList();

        // Se nada passou pelo filtro, escolha as top N por similaridade semântica
        // como fallback para evitar síntese vazia.
        if (filtradas.Count == 0 && vetorConsulta is not null)
        {
            var topSem = avaliadasComSemantica
                .OrderByDescending(x => x.Semantica)
                .ThenByDescending(x => EhFonteConfiavel(x.Fonte.Url))
                .Take(5)
                .Select(x => x.Fonte)
                .ToList();

            filtradas.AddRange(topSem);
        }

        fontes.Clear();
        fontes.AddRange(filtradas);
    }

    private async Task<int> CalcularPontuacaoFonteAsync(
        ResultadoPesquisaWeb fonte,
        IReadOnlyCollection<string> termos,
        float[]? vetorConsulta,
        Dictionary<string, float[]> vetoresFontes,
        CancellationToken cancellationToken)
    {
        var titulo = NormalizarTexto(fonte.Titulo);
        var trecho = NormalizarTexto(fonte.Trecho);
        var url = NormalizarTexto(fonte.Url);
        var textoFonte = $"{titulo} {trecho} {url}";
        var pontos = 0;
        var acertosTitulo = 0;
        var acertosConteudo = 0;
        var semanticaForte = false;

        foreach (var termo in termos)
        {
            if (TextoContemTermo(titulo, termo))
            {
                pontos += 5;
                acertosTitulo++;
            }
            else if (TextoContemTermo(trecho, termo))
            {
                pontos += 2;
                acertosConteudo++;
            }

            if (TextoContemTermo(url, termo))
                pontos++;
        }

        if (EhFonteConfiavel(fonte.Url))
            pontos += 2;

        if (vetorConsulta is not null)
        {
            try
            {
                if (!vetoresFontes.TryGetValue(fonte.Url, out var vetorFonte))
                {
                    vetorFonte = await _embedding.GerarAsync(textoFonte, cancellationToken);
                    vetoresFontes[fonte.Url] = vetorFonte;
                }
                var semantica = Cosine(vetorConsulta, vetorFonte);
                if (semantica >= 0.82)
                {
                    pontos += 10;
                    semanticaForte = true;
                }
                else if (semantica >= 0.72)
                {
                    pontos += 5;
                    semanticaForte = true;
                }
                else if (semantica < 0.45 && acertosTitulo == 0 && acertosConteudo == 0) return 0;
            }
            catch
            {
            }
        }

        var tecnologiasDotNet = IdentificarTermosEcossistemaDotNet(string.Join(' ', termos));
        if (tecnologiasDotNet.Length > 0 &&
            !tecnologiasDotNet.Any(tecnologia =>
                titulo.Contains(tecnologia, StringComparison.OrdinalIgnoreCase) ||
                trecho.Contains(tecnologia, StringComparison.OrdinalIgnoreCase)))
            return 0;

        // Conceito composto: "linguagem de máquina" precisa aparecer como assunto
        // da fonte, acompanhado de contexto computacional. Palavras isoladas no
        // título, no trecho ou na URL não bastam para validar o conceito.
        if (termos.Contains("linguagem", StringComparer.OrdinalIgnoreCase) &&
            termos.Contains("maquina", StringComparer.OrdinalIgnoreCase))
        {
            var conceitoComposto = new[] { "linguagem", "maquina" };
            var conceitosComputacao = new[]
            {
                "computador", "computacao", "informatica", "programa", "instrucao",
                "codigo", "binario", "processador", "software", "hardware", "memoria"
            };

            var conceitoPresente =
                TermosConcentrados(titulo, conceitoComposto) ||
                TermosConcentrados(trecho, conceitoComposto);
            var contextoComputacionalPresente =
                conceitosComputacao.Any(termo =>
                    TextoContemTermo(titulo, termo) || TextoContemTermo(trecho, termo));

            if (!conceitoPresente || !contextoComputacionalPresente)
                return 0;
        }

        // Consultas naturais incluem verbos, pedidos de exemplo e qualificadores.
        // Exigir todos os termos exatos elimina fontes válidas que cobrem o conceito central.
        // Para consultas com 1–2 termos, exigir todos; para consultas maiores, exigir ao menos 2.
        var coberturaMinima = termos.Count <= 2
            ? termos.Count
            : 2;
        // Não exija que os termos apareçam numa janela de apenas sete palavras.
        // Resultados relevantes frequentemente distribuem os termos entre título e trecho.
        var coberturaDireta = acertosTitulo + acertosConteudo >= coberturaMinima;
        var coberturaSemantica = termos.Count == 1 && acertosTitulo >= 1 && semanticaForte;
        return coberturaDireta || coberturaSemantica ? pontos : 0;
    }

    private async Task OrdenarFontesPorSemanticaAsync(
        List<ResultadoPesquisaWeb> fontes,
        float[] vetorConsulta,
        Dictionary<string, float[]> vetoresFontes,
        CancellationToken cancellationToken)
    {
        var ranqueadas = (await Task.WhenAll(fontes.Select(async fonte =>
        {
            try
            {
                if (!vetoresFontes.TryGetValue(fonte.Url, out var vetorFonte))
                {
                    var texto = $"{fonte.Titulo} {fonte.Trecho}";
                    vetorFonte = await _embedding.GerarAsync(texto, cancellationToken);
                    vetoresFontes[fonte.Url] = vetorFonte;
                }

                return (Fonte: fonte, Score: Cosine(vetorConsulta, vetorFonte));
            }
            catch
            {
                return (Fonte: fonte, Score: 0d);
            }
        })))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => EhFonteConfiavel(x.Fonte.Url))
            .Take(12)
            .Select(x => x.Fonte)
            .ToList();

        fontes.Clear();
        fontes.AddRange(ranqueadas);
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double produto = 0;
        double normaA = 0;
        double normaB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            produto += a[i] * b[i];
            normaA += a[i] * a[i];
            normaB += b[i] * b[i];
        }

        return normaA == 0 || normaB == 0
            ? 0
            : produto / (Math.Sqrt(normaA) * Math.Sqrt(normaB));
    }

    private static string ConstruirConsultaBusca(string consulta, IReadOnlyCollection<string> termos)
    {
        var consultaNatural = (consulta ?? string.Empty).Trim();
        var normalizada = NormalizarTexto(consultaNatural);

        if (normalizada.Contains("linguagem", StringComparison.OrdinalIgnoreCase) &&
            normalizada.Contains("maquina", StringComparison.OrdinalIgnoreCase))
            return "\"linguagem de máquina\" \"código de máquina\" computador binário";

        if (IdentificarTermosEcossistemaDotNet(consultaNatural).Length > 0)
            return $"{consultaNatural} .NET C#";

        if (normalizada.Contains("gemini", StringComparison.OrdinalIgnoreCase))
        {
            var consultaSobreModelos = normalizada.Contains("modelo", StringComparison.OrdinalIgnoreCase) ||
                                       normalizada.Contains("documentacao", StringComparison.OrdinalIgnoreCase) ||
                                       normalizada.Contains("versao", StringComparison.OrdinalIgnoreCase) ||
                                       normalizada.Contains("atual", StringComparison.OrdinalIgnoreCase) ||
                                       normalizada.Contains("mais recente", StringComparison.OrdinalIgnoreCase) ||
                                       normalizada.Contains("lista", StringComparison.OrdinalIgnoreCase);

            // Para perguntas sobre documentação/modelos, uma consulta longa junto de "Google Gemini API"
            // estava levando o Bing a devolver resultados genéricos e sem relação com o Gemini.
            if (consultaSobreModelos)
                return consultaNatural;

            return $"{consultaNatural} Google Gemini API";
        }

        // Preserve a pergunta em linguagem natural. Remover palavras de ligação
        // como "do", "da", "qual" e "a" altera o significado de consultas
        // como "qual é a capital do Brasil" e pode produzir resultados de outro assunto.
        if (!string.IsNullOrWhiteSpace(consultaNatural))
            return consultaNatural;

        return string.Join(' ', termos);
    }
    private static string[] IdentificarTermosEcossistemaDotNet(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return Array.Empty<string>();

        var normalizada = NormalizarTexto(texto);
        var tecnologias = new List<string>();

        if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"(?i)(?<![\p{L}\p{N}])(?:\.net|dotnet|c#|f#|vb\.net)(?![\p{L}\p{N}])") ||
            System.Text.RegularExpressions.Regex.IsMatch(normalizada, @"(?i)(?<![\p{L}\p{N}])net(?![\p{L}\p{N}])"))
            tecnologias.Add("net");

        var termosConhecidos = new[]
        {
            "entity framework", "ef core", "linq", "asp net", "blazor",
            "maui", "wpf", "avalonia"
        };

        foreach (var termo in termosConhecidos)
        {
            if (normalizada.Contains(termo, StringComparison.OrdinalIgnoreCase))
                tecnologias.Add(termo);
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"(?i)(?<![\p{L}\p{N}])c#(?![\p{L}\p{N}])"))
            tecnologias.Add("c#");
        if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"(?i)(?<![\p{L}\p{N}])f#(?![\p{L}\p{N}])"))
            tecnologias.Add("f#");

        return tecnologias.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static bool EhConsultaPuramenteConversa(string consulta)
    {
        var texto = NormalizarTexto(consulta);
        return texto is "ola" or "oi" or "bom dia" or "boa tarde" or "boa noite";
    }

    private static string[] ExtrairTermos(string texto)
    {
        var normalizado = NormalizarTexto(texto);
        var palavras = normalizado.Split(
            new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries);

        // Excluir somente palavras funcionais e comandos de pesquisa.
        // Não remover termos de assunto, qualificadores temporais ou critérios de comparação.
        var ignorar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "o", "e", "de", "do", "da", "dos", "das", "que", "qual", "quais", "como",
            "uma", "um", "para", "com", "por", "em", "no", "na", "nos", "nas", "sobre",
            "quem", "informacao", "informacoes", "encontrada", "encontradas", "fonte", "fontes",
            "pesquise", "pesquisar", "pesquisa", "procure", "busque", "internet", "web",
            "apresente", "apresentar", "consultada", "consultadas", "consultado", "consultados",
            "pesquisada", "pesquisadas", "pesquisado", "pesquisados", "resposta", "respostas",
            "sintese", "resumo", "inclua", "incluindo", "porfavor"
        };

        return palavras
            .Where(p => (p.Length >= 3 || p.Equals("c#", StringComparison.OrdinalIgnoreCase)) && !ignorar.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static string[] ExtrairTermosFatuais(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return Array.Empty<string>();

        var normalizado = texto
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray();

        var palavras = new string(normalizado)
            .Split(
                new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\', '(', ')', '[', ']', '*', '"' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(p => new string(
                p.Where(char.IsLetterOrDigit).ToArray())
                .ToLowerInvariant())
            .Where(p => p.Length >= 5)
            .ToArray();

        var ignorar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cultura", "bolivia", "profundamente", "marcada", "marcado", "resulta", "resultado",
            "combina", "combinacao", "diversa", "diverso", "diversidade", "caracterizada",
            "caracterizado", "seguinte", "seguintes", "pontos", "pais", "possui", "possuir",
            "forma", "formas", "varios", "varias", "forte", "fortemente", "tradicional",
            "tradicionais", "tambem", "sendo", "incluindo", "principalmente", "frequente",
            "frequentemente", "reconhecida", "reconhecido", "elemento", "elementos", "papel",
            "social", "sociais", "praticas", "pratica", "regiao", "regioes", "povo", "povos"
        };

        return palavras
            .Where(p => !ignorar.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static bool TermosConcentrados(
        string texto,
        IReadOnlyCollection<string> termos,
        int minimoCorrespondencias = 0)
    {
        var palavras = NormalizarTexto(texto)
            .Split(
                new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);

        if (termos.Count == 0 || palavras.Length == 0)
            return false;

        var minimo = minimoCorrespondencias <= 0
            ? termos.Count
            : Math.Min(minimoCorrespondencias, termos.Count);
        const int janela = 7;
        for (var inicio = 0; inicio < palavras.Length; inicio++)
        {
            var fim = Math.Min(palavras.Length, inicio + janela);
            var trecho = palavras[inicio..fim];
            var correspondencias = termos.Count(termo =>
                trecho.Any(palavra =>
                    palavra.Equals(termo, StringComparison.OrdinalIgnoreCase)));
            if (correspondencias >= minimo)
                return true;
        }

        return false;
    }

    private static bool TextoContemTermo(string? texto, string termo)
    {
        var palavras = NormalizarTexto(texto ?? string.Empty)
            .Split(
                new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);

        return palavras.Any(palavra =>
            palavra.Equals(termo, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizarTexto(string texto) =>
        new string((texto ?? string.Empty)
            .ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());

    private static bool ExisteFonteFortementeRelacionada(
        IEnumerable<ResultadoPesquisaWeb> fontes,
        IReadOnlyCollection<string> termos)
    {
        if (termos.Count == 0)
            return fontes.Any();

        return fontes.Any(fonte =>
        {
            var titulo = termos.Count(termo => TextoContemTermo(fonte.Titulo, termo));
            var trecho = termos.Count(termo => TextoContemTermo(fonte.Trecho, termo));
            var url = termos.Count(termo => TextoContemTermo(fonte.Url, termo));
            return (titulo >= 1 && trecho >= 1) || titulo >= 2 || (EhFonteConfiavel(fonte.Url) && (titulo >= 1 || trecho >= 2));
        });
    }

    private static bool EhFonteConfiavel(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host.EndsWith(".gov.br", StringComparison.Ordinal) ||
               host.EndsWith(".edu.br", StringComparison.Ordinal) ||
               host.Contains("microsoft.com", StringComparison.Ordinal) ||
               host.Contains("github.com", StringComparison.Ordinal);
    }

    private async Task Buscar(
        string consulta,
        List<ResultadoPesquisaWeb> fontes,
        CancellationToken ct)
    {
        try
        {
            var resultados = await _web.PesquisarAsync(consulta, 10, ct);
            foreach (var item in resultados)
            {
                if (!fontes.Any(x => x.Url.Equals(item.Url, StringComparison.OrdinalIgnoreCase)))
                    fontes.Add(item);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha na busca web: {Consulta}", consulta);
        }
    }
    private async Task<Resposta> ModeloAuxiliar(
        string texto,
        IContexto contexto,
        CancellationToken ct)
    {
        try
        {
            return await _modelo.GerarAsync(
                new Instrucao { Conteudo = texto },
                contexto,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Modelo indisponível durante Deep Search.");
            return new Resposta { Conteudo = string.Empty };
        }
    }

    private static string NormalizarConsulta(string texto)
    {
        var consulta = (texto ?? string.Empty).Trim();
        consulta = Regex.Replace(
            consulta,
            @"^(?:(?:ol[áa])\s*[,!:;-]?\s+)?kayron\b[\s,.:;!?—-]*",
            string.Empty,
            RegexOptions.IgnoreCase);
        consulta = Regex.Replace(consulta, @"\bvenesuela\b", "Venezuela", RegexOptions.IgnoreCase);
        var prefixos = new[]
        {
            "olá, caro, faça uma pesquisa na internet sobre ",
            "ola, caro, faca uma pesquisa na internet sobre ",
            "faça uma pesquisa na internet sobre ",
            "faca uma pesquisa na internet sobre ",
            "pesquisa na internet: ", "pesquisa na internet ", "pesquisa na web: ", "pesquisa na web ",
            "pesquise na internet: ", "pesquise na internet ", "pesquise na web: ", "pesquise na web ",
            "pesquisar na internet: ", "pesquisar na internet ", "pesquisar na web: ", "pesquisar na web ",
            "procure na internet ", "procure na web ",
            "busque na internet ", "busque na web ", "pesquise ", "pesquisar ", "pesquisa ", "procure ", "busque "
        };

        bool prefixoRemovido;
        do
        {
            prefixoRemovido = false;
            foreach (var prefixo in prefixos)
            {
                if (consulta.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                {
                    consulta = consulta[prefixo.Length..].Trim();
                    prefixoRemovido = true;
                    break;
                }
            }
        } while (prefixoRemovido);

        var sufixos = new[]
        {
            " e cite as fontes",
            " e cite fontes",
            " e me responda com as informações encontradas nas fontes pesquisadas",
            " e me responda com as informacoes encontradas nas fontes pesquisadas",
            " e me responda com as informações encontradas nas fontes",
            " e me responda com as informacoes encontradas nas fontes",
            " com as informações encontradas nas fontes pesquisadas",
            " com as informacoes encontradas nas fontes pesquisadas"
        };

        foreach (var sufixo in sufixos)
        {
            if (consulta.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta[..^sufixo.Length].Trim();
                break;
            }
        }

        foreach (var sufixo in new[] { ", por favor.", " por favor.", ", por favor", " por favor" })
        {
            if (consulta.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta[..^sufixo.Length].Trim();
                break;
            }
        }

        return consulta;
    }

    private static IEnumerable<string> Consultas(string texto, string original)
    {
        foreach (var linha in (texto ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var q = linha.Trim().TrimStart('-', '*', '1', '2', '.', ' ');
            if (q.Length >= 8 && q.Length <= 180 &&
                !q.Equals(original, StringComparison.OrdinalIgnoreCase))
                yield return q;
        }
    }

    private static string Fontes(IEnumerable<ResultadoPesquisaWeb> fontes)
    {
        var sb = new StringBuilder();
        var i = 1;
        foreach (var fonte in fontes.Take(12))
        {
            sb.AppendLine($"[{i++}] {fonte.Titulo}");
            sb.AppendLine($"URL: {fonte.Url}");
            if (!string.IsNullOrWhiteSpace(fonte.Trecho))
                sb.AppendLine($"TRECHO: {fonte.Trecho}");
        }
        return sb.Length == 0 ? "Nenhuma fonte encontrada." : sb.ToString();
    }
    private static bool EhEvidenciaInsuficiente(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return true;

        var marcadores = new[]
        {
            "não disponho de base suficiente",
            "nao disponho de base suficiente",
            "os dados recuperados sao insuficientes",
            "os dados recuperados são insuficientes",
            "dados recuperados insuficientes",
            "as fontes recuperadas sao insuficientes",
            "as fontes recuperadas são insuficientes",
            "não contém informações",
            "nao contem informacoes",
            "não contém informação",
            "nao contem informacao",
            "não foi possível confirmar",
            "nao foi possivel confirmar",
            "não consegui confirmar",
            "nao consegui confirmar",
            "evidência recuperada na pesquisa",
            "evidencia recuperada na pesquisa",
            "fontes não sustentam",
            "fontes nao sustentam",
            "evidência insuficiente",
            "evidencia insuficiente"
        };

        return marcadores.Any(marcador =>
            texto.Contains(marcador, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EhRespostaPesquisaValida(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var marcadoresDeRespostaSemPesquisa = new[]
        {
            "baseada em conhecimentos gerais",
            "com base em conhecimentos gerais",
            "não houve um resultado de ferramenta",
            "não houve resultado de ferramenta",
            "posso realizar uma busca",
            "deseja que eu faça isso",
            "posso pesquisar para você",
            "não foi possível realizar a pesquisa",
            "identificou uma conversa:",
            "[KAYRON_DECISAO]",
            "\"proxima_acao\"",
            "\"parametros\""
        };

        return !marcadoresDeRespostaSemPesquisa.Any(marcador =>
            texto.Contains(marcador, StringComparison.OrdinalIgnoreCase));
    }

    private Resposta AnexarFontes(
        Resposta resposta,
        string consulta,
        IEnumerable<ResultadoPesquisaWeb> fontes,
        bool verificacaoFonte)
    {
        var conteudo = resposta.Conteudo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(conteudo))
            return resposta;

        var fontesLista = fontes.ToList();
        _logger.LogInformation("DEEPSEARCH AnexarFontes: fontes_lista={Quantidade}; consulta={Consulta}", fontesLista.Count, consulta);
        for (var i = 0; i < fontesLista.Count; i++)
            _logger.LogInformation("DEEPSEARCH MAPA_FONTE: indice={Indice}; titulo={Titulo}; url={Url}", i + 1, fontesLista[i].Titulo, fontesLista[i].Url);
        var secaoFontes = Regex.Match(
            conteudo,
            "(?im)^\\s*(?:#{1,6}\\s*)?\\*{0,2}(?:fontes(?: consultadas| utilizadas)?|referências)\\*{0,2}\\s*:?.*$");
        var corpo = secaoFontes.Success
            ? conteudo[..secaoFontes.Index].TrimEnd()
            : conteudo;
        var indicesCitados = new HashSet<int>();

        foreach (Match match in Regex.Matches(
                     corpo,
                     "\\[FONTE\\s+(\\d+)\\]",
                     RegexOptions.IgnoreCase))
        {
            if (int.TryParse(match.Groups[1].Value, out var indice) &&
                indice >= 1 && indice <= fontesLista.Count)
                indicesCitados.Add(indice);
        }

        foreach (Match match in Regex.Matches(
                     corpo,
                     "\\[(\\d{1,2}(?:\\s*,\\s*\\d{1,2})*)\\]"))
        {
            foreach (var parte in match.Groups[1].Value.Split(','))
            {
                if (int.TryParse(parte.Trim(), out var indice) &&
                    indice >= 1 && indice <= fontesLista.Count)
                    indicesCitados.Add(indice);
            }
        }

        _logger.LogInformation("DEEPSEARCH AnexarFontes: indices_citados={Quantidade}; corpo_tem_marcador={TemMarcador}", indicesCitados.Count, corpo.Contains("[FONTE", StringComparison.OrdinalIgnoreCase));

        // Remove os marcadores do texto e mantém apenas a síntese para leitura contínua.
        corpo = Regex.Replace(corpo, "\\[FONTE\\s+\\d+\\]", string.Empty, RegexOptions.IgnoreCase);
        corpo = Regex.Replace(corpo, "\\[(\\d{1,2}(?:\\s*,\\s*\\d{1,2})*)\\]", string.Empty);
        corpo = Regex.Replace(
            corpo,
            @"\[(?<rotulo>[^\]]+)\]\(https?://[^)\s]+\)",
            "${rotulo}",
            RegexOptions.IgnoreCase);
        corpo = Regex.Replace(corpo, @"https?://[^\s<>]+", string.Empty, RegexOptions.IgnoreCase);
        corpo = Regex.Replace(corpo, @"[ \t]{2,}", " ");
        corpo = Regex.Replace(corpo, @"[ \t]+([.,;:!?])", "$1");
        corpo = Regex.Replace(corpo, @"(?m)^\s*[-*]\s*$", string.Empty);
        corpo = corpo.Trim();

        var resultado = new StringBuilder(corpo);
        var fontesCitadas = indicesCitados
            .OrderBy(indice => indice)
            .Select(indice => fontesLista[indice - 1])
            .DistinctBy(fonte => fonte.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Se o modelo omitir os marcadores, não esconda as fontes recuperadas pela pesquisa.
        if (fontesCitadas.Count == 0)
        {
            fontesCitadas = fontesLista
                .Where(fonte => Uri.TryCreate(fonte.Url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .DistinctBy(fonte => fonte.Url, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
        }

        _logger.LogInformation("DEEPSEARCH AnexarFontes: fontes_citadas_final={Quantidade}; fallback_aplicado={Fallback}", fontesCitadas.Count, indicesCitados.Count == 0 && fontesCitadas.Count > 0);

        if (fontesCitadas.Count > 0)
        {
            if (resultado.Length > 0)
                resultado.AppendLine().AppendLine();
            resultado.AppendLine("**Fontes**");
            foreach (var fonte in fontesCitadas)
            {
                var titulo = (fonte.Titulo ?? string.Empty)
                    .Replace("[", "(", StringComparison.Ordinal)
                    .Replace("]", ")", StringComparison.Ordinal);
                var url = (fonte.Url ?? string.Empty)
                    .Replace(" ", "%20", StringComparison.Ordinal)
                    .Replace("(", "%28", StringComparison.Ordinal)
                    .Replace(")", "%29", StringComparison.Ordinal);
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    resultado.AppendLine($"- [{titulo}]({url})");
                else
                    resultado.AppendLine($"- {titulo}");
            }
        }

        return new Resposta
        {
            Conteudo = resultado.ToString().Trim()
        };
    }

    private static string FormatarLinkFonte(ResultadoPesquisaWeb fonte)
    {
        if (!Uri.TryCreate(fonte.Url, UriKind.Absolute, out var uri))
            return fonte.Titulo;

        var host = uri.Host.ToLowerInvariant();
        var nome = host switch
        {
            var h when h.EndsWith("wikipedia.org", StringComparison.Ordinal) => "Wikipédia",
            var h when h.EndsWith("brasilescola.uol.com.br", StringComparison.Ordinal) => "Brasil Escola",
            var h when h.EndsWith("britannica.com", StringComparison.Ordinal) => "Encyclopaedia Britannica",
            var h when h.EndsWith("gov.br", StringComparison.Ordinal) => "Gov.br",
            var h when h.EndsWith("fgv.br", StringComparison.Ordinal) => "FGV",
            var h when h.EndsWith("escolaeducacao.com.br", StringComparison.Ordinal) => "Escola Educação",
            _ => host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host
        };

        var url = fonte.Url
            .Replace(" ", "%20", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal);
        return $"[{nome}]({url})";
    }
    private static string Fallback(string consulta, IEnumerable<ResultadoPesquisaWeb> fontes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Fontes recuperadas da pesquisa web para: {consulta}");
        sb.AppendLine();
        foreach (var fonte in fontes.Take(10))
        {
            sb.AppendLine($"- {FormatarLinkFonte(fonte)}");
            if (!string.IsNullOrWhiteSpace(fonte.Trecho))
                sb.AppendLine($"  Trecho: {fonte.Trecho}");
        }

        return sb.ToString().Trim();
    }
}
