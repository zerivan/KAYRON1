using System.Text;
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

        var vetorConsulta = await _embedding.GerarAsync(consulta, cancellationToken);
        var vetoresFontes = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        var consultaBusca = ConstruirConsultaBusca(consulta, termosConsulta);

        await Buscar(consultaBusca, fontes, cancellationToken);
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

        var prompt = new StringBuilder();
        prompt.AppendLine("MODO: ANÁLISE DE PESQUISA DO KAYRON.");
        prompt.AppendLine("O agente KAYRON já filtrou as fontes. Você é apenas o sintetizador dos dados recuperados.");
        prompt.AppendLine("A pesquisa web já foi executada. Analise exclusivamente os dados recuperados e devolva uma síntese factual para o KAYRON.");
        prompt.AppendLine("Use SOMENTE as informações sustentadas pelos trechos das fontes abaixo.");
        prompt.AppendLine("NÃO use conhecimento geral, memória do modelo, histórico da conversa ou conhecimento prévio.");
        prompt.AppendLine("NÃO diga que já possui a informação em uma base de conhecimento.");
        prompt.AppendLine("NÃO diga que a pesquisa não foi realizada e NÃO ofereça realizar outra pesquisa.");
        prompt.AppendLine("Não responda sobre assuntos que não estejam sustentados pelas fontes recuperadas.");
        prompt.AppendLine("Se as fontes não sustentarem uma resposta factual suficiente, informe exatamente isso.");
        prompt.AppendLine("Produza somente a síntese factual necessária para o KAYRON; não converse diretamente com o usuário.");
        prompt.AppendLine("Inclua uma seção Fontes com título e URL.");
        prompt.AppendLine();
        prompt.AppendLine("PERGUNTA:");
        prompt.AppendLine(consulta);
        prompt.AppendLine();
        prompt.AppendLine("FONTES:");
        prompt.AppendLine(Fontes(fontes));

        var contextoAnalise = new Contexto();
        contextoAnalise.Adicionar("entrada_original", consulta);
        contextoAnalise.Adicionar("intencao_detectada", "internet:pesquisar");

        Resposta? respostaInicial = null;

        try
        {
            var resposta = await ModeloAuxiliar(prompt.ToString(), contextoAnalise, cancellationToken);
            respostaInicial = resposta;
            if (EhRespostaPesquisaValida(resposta.Conteudo) &&
                !EhEvidenciaInsuficiente(resposta.Conteudo))
                return resposta;

            // Se a primeira pesquisa não sustentar a resposta, não encerre o turno.
            // Refaz a busca com consultas de contexto antes de admitir falta de evidência.
            foreach (var consultaAlternativa in new[]
                     {
                         $"{consulta} contexto histórico fatos principais",
                         $"{consulta} fontes oficiais história contexto"
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
                prompt.AppendLine("Use SOMENTE as informações sustentadas pelos trechos das fontes abaixo.");
                prompt.AppendLine("Não use memória, conhecimento prévio ou suposições.");
                prompt.AppendLine("Responda diretamente à pergunta e inclua os fatos históricos necessários.");
                prompt.AppendLine("Se ainda houver lacunas, faça a melhor síntese possível apenas com o que estiver sustentado pelas fontes.");
                prompt.AppendLine("Inclua uma seção Fontes com título e URL.");
                prompt.AppendLine();
                prompt.AppendLine("PERGUNTA:");
                prompt.AppendLine(consulta);
                prompt.AppendLine();
                prompt.AppendLine("FONTES:");
                prompt.AppendLine(Fontes(fontes));

                var novaResposta = await ModeloAuxiliar(prompt.ToString(), contextoAnalise, cancellationToken);
                if (EhRespostaPesquisaValida(novaResposta.Conteudo) &&
                    !EhEvidenciaInsuficiente(novaResposta.Conteudo))
                    return novaResposta;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Síntese Gemini indisponível; usando somente conteúdo recuperado.");
        }

        if (respostaInicial is not null && !string.IsNullOrWhiteSpace(respostaInicial.Conteudo))
            return respostaInicial;

        var fallback = Fallback(consulta, fontes);
        return new Resposta { Conteudo = EhRespostaPesquisaValida(fallback) ? fallback : string.Empty };
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

        foreach (var avaliada in avaliadas.OrderByDescending(x => x.Pontos).Take(12))
            _logger.LogInformation("DEEPSEARCH FONTE: pontos={Pontos}; titulo={Titulo}; url={Url}", avaliada.Pontos, avaliada.Fonte.Titulo, avaliada.Fonte.Url);

        var filtradas = avaliadas
            .Where(x => x.Pontos >= 2)
            .OrderByDescending(x => x.Pontos)
            .ThenByDescending(x => EhFonteConfiavel(x.Fonte.Url))
            .Take(12)
            .Select(x => x.Fonte)
            .ToList();

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

        if (termos.Contains("net", StringComparer.OrdinalIgnoreCase) &&
            termos.Contains("c#", StringComparer.OrdinalIgnoreCase))
        {
            var conceitosDotNet = new[] { "net", "c#" };
            if (!TermosConcentrados(titulo, conceitosDotNet) &&
                !TermosConcentrados(trecho, conceitosDotNet))
                return 0;
        }

        var coberturaMinima = Math.Min(2, termos.Count);
        var coberturaDireta = acertosTitulo + acertosConteudo >= coberturaMinima ||
            (TermosConcentrados(titulo, termos) || TermosConcentrados(trecho, termos));
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

        if (normalizada.Contains(".net", StringComparison.OrdinalIgnoreCase) ||
            normalizada.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
            return $"{consultaNatural} .NET C#";

        if (normalizada.Contains("gemini", StringComparison.OrdinalIgnoreCase))
            return $"{consultaNatural} Google Gemini API";

        // Preserve a pergunta em linguagem natural. Remover palavras de ligação
        // como "do", "da", "qual" e "a" altera o significado de consultas
        // como "qual é a capital do Brasil" e pode produzir resultados de outro assunto.
        if (!string.IsNullOrWhiteSpace(consultaNatural))
            return consultaNatural;

        return string.Join(' ', termos);
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

        var ignorar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "o", "e", "de", "do", "da", "dos", "das", "que", "qual", "quais", "como",
            "uma", "um", "para", "com", "por", "em", "no", "na", "nos", "nas", "mais", "usada",
            "usado", "usadas", "usados", "principal", "sobre", "atual", "atualmente", "hoje",
            "quem", "qual", "quais", "informacao", "informacoes", "encontrada", "encontradas", "fonte", "fontes",
            "pesquise", "pesquisar", "pesquisa", "procure", "busque", "internet", "web", "oficiais", "dados", "recentes",
            "linguagem", "linguagens", "principal", "principais", "usada", "usado", "usadas", "usados",
            "utilizada", "utilizado", "utilizadas", "utilizados", "utilizar", "utiliza", "normalmente",
            "desenvolver", "desenvolvimento", "aplicacao", "aplicacoes", "programacao", "informacao", "informacoes"
        };

        return palavras
            .Where(p => (p.Length >= 3 || p.Equals("c#", StringComparison.OrdinalIgnoreCase)) && !ignorar.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
    }

    private static bool TermosConcentrados(string texto, IReadOnlyCollection<string> termos)
    {
        var palavras = NormalizarTexto(texto)
            .Split(
                new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);

        if (termos.Count == 0 || palavras.Length == 0)
            return false;

        const int janela = 7;
        for (var inicio = 0; inicio < palavras.Length; inicio++)
        {
            var fim = Math.Min(palavras.Length, inicio + janela);
            var trecho = palavras[inicio..fim];
            if (termos.All(termo =>
                trecho.Any(palavra =>
                    palavra.Equals(termo, StringComparison.OrdinalIgnoreCase))))
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
            var resultados = await _web.PesquisarAsync(consulta, 6, ct);
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
        var prefixos = new[]
        {
            "pesquisa na internet: ", "pesquisa na internet ", "pesquisa na web: ", "pesquisa na web ",
            "pesquise na internet ", "pesquise na web ", "pesquisar na internet ", "pesquisar na web ",
            "procure na internet ", "procure na web ",
            "busque na internet ", "busque na web ", "pesquise ", "pesquisar ", "pesquisa ", "procure ", "busque "
        };

        foreach (var prefixo in prefixos)
        {
            if (consulta.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta[prefixo.Length..].Trim();
                break;
            }
        }

        var sufixos = new[]
        {
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

    private static string Fallback(string consulta, IEnumerable<ResultadoPesquisaWeb> fontes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Fontes recuperadas da pesquisa web para: {consulta}");
        sb.AppendLine();
        foreach (var fonte in fontes.Take(10))
        {
            sb.AppendLine($"- {fonte.Titulo}");
            sb.AppendLine($"  URL: {fonte.Url}");
            if (!string.IsNullOrWhiteSpace(fonte.Trecho))
                sb.AppendLine($"  Trecho: {fonte.Trecho}");
        }

        return sb.ToString().Trim();
    }
}
