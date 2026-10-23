using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace KAYRON.Engine;

public sealed record ResultadoPesquisaWeb(
    string Titulo,
    string Url,
    string Trecho);

public sealed class PesquisaWeb
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PesquisaWeb> _logger;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);
    private const int MaxConteudoPagina = 12000;

    public PesquisaWeb(HttpClient httpClient, ILogger<PesquisaWeb> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(decimal Compra, decimal Venda, DateTime DataHora)?> ObterCotacaoDolarAsync(
        CancellationToken cancellationToken = default)
    {
        for (var dias = 0; dias <= 7; dias++)
        {
            var data = DateTime.Today.AddDays(-dias);
            var dataFormatada = data.ToString("MM-dd-yyyy");
            var url =
                "https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/" +
                $"CotacaoDolarDia(dataCotacao=@dataCotacao)?@dataCotacao='{dataFormatada}'&$top=100&$format=json";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
            using var response = await SendWithTimeoutAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                continue;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var documento = JsonDocument.Parse(json);
            if (!documento.RootElement.TryGetProperty("value", out var valores) ||
                valores.ValueKind != JsonValueKind.Array || valores.GetArrayLength() == 0)
                continue;

            var ultimo = valores.EnumerateArray().Last();
            if (!ultimo.TryGetProperty("cotacaoCompra", out var compra) ||
                !ultimo.TryGetProperty("cotacaoVenda", out var venda) ||
                !ultimo.TryGetProperty("dataHoraCotacao", out var dataHora))
                continue;

            var textoCompra = compra.ValueKind == JsonValueKind.Number
                ? compra.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : compra.GetString();
            var textoVenda = venda.ValueKind == JsonValueKind.Number
                ? venda.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : venda.GetString();
            var textoDataHora = dataHora.ValueKind == JsonValueKind.String ? dataHora.GetString() : null;

            if (!decimal.TryParse(textoCompra, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valorCompra) ||
                !decimal.TryParse(textoVenda, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valorVenda) ||
                !DateTime.TryParse(textoDataHora, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var valorDataHora))
                continue;

            return (valorCompra, valorVenda, valorDataHora);
        }

        return null;
    }

    public async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarAsync(
        string consulta,
        int limite = 6,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return Array.Empty<ResultadoPesquisaWeb>();

        var consultaLimpa = consulta.Trim();
        consultaLimpa = NormalizarConsulta(consultaLimpa);
        var limiteReal = Math.Clamp(limite, 1, 10);

        var resultadosDiversificados = new List<ResultadoPesquisaWeb>();

        var braveKey = Environment.GetEnvironmentVariable("BRAVE_SEARCH_API_KEY");
        if (!string.IsNullOrWhiteSpace(braveKey))
        {
            var resultados = await PesquisarBraveAsync(consultaLimpa, limiteReal, braveKey, cancellationToken);
            _logger.LogInformation("PESQUISAWEB PROVEDOR=Brave; consulta={Consulta}; resultados_brutos={Quantidade}", consultaLimpa, resultados.Count);
            resultados = await EnriquecerResultadosAsync(resultados, consultaLimpa, cancellationToken);
            _logger.LogInformation("PESQUISAWEB PROVEDOR=Brave; resultados_enriquecidos={Quantidade}", resultados.Count);
            AdicionarResultadosDiversificados(resultadosDiversificados, resultados, limiteReal);
            if (TemDiversidadeSuficiente(resultadosDiversificados, limiteReal))
                return AplicarRestricaoDominioSolicitado(consultaLimpa, resultadosDiversificados);
        }

        var tavilyKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (!string.IsNullOrWhiteSpace(tavilyKey))
        {
            var resultados = await PesquisarTavilyAsync(consultaLimpa, limiteReal, tavilyKey, cancellationToken);
            _logger.LogInformation("PESQUISAWEB PROVEDOR=Tavily; consulta={Consulta}; resultados_brutos={Quantidade}", consultaLimpa, resultados.Count);
            resultados = await EnriquecerResultadosAsync(resultados, consultaLimpa, cancellationToken);
            _logger.LogInformation("PESQUISAWEB PROVEDOR=Tavily; resultados_enriquecidos={Quantidade}", resultados.Count);
            AdicionarResultadosDiversificados(resultadosDiversificados, resultados, limiteReal);
            if (TemDiversidadeSuficiente(resultadosDiversificados, limiteReal))
                return AplicarRestricaoDominioSolicitado(consultaLimpa, resultadosDiversificados);
        }

        var google = await PesquisarGoogleAsync(consultaLimpa, limiteReal, cancellationToken);
        var googleRelevante = TemRelevanciaBasica(google, consultaLimpa);
        _logger.LogInformation("PESQUISAWEB PROVEDOR=Google; consulta={Consulta}; resultados_brutos={Quantidade}; relevancia_basica={Relevante}", consultaLimpa, google.Count, googleRelevante);
        if (googleRelevante)
        {
            var resultados = await EnriquecerResultadosAsync(google, consultaLimpa, cancellationToken);
            AdicionarResultadosDiversificados(resultadosDiversificados, resultados, limiteReal);
            if (TemDiversidadeSuficiente(resultadosDiversificados, limiteReal))
                return AplicarRestricaoDominioSolicitado(consultaLimpa, resultadosDiversificados);
        }


        if (resultadosDiversificados.Count > 0)
            return AplicarRestricaoDominioSolicitado(consultaLimpa, resultadosDiversificados);

        // Uma consulta com site: exige respeitar o domínio solicitado.
        // Não usar a Wikipédia como fallback para esse tipo de consulta.
        if (Regex.IsMatch(consultaLimpa, @"(?:^|\s)site:[^\s]+", RegexOptions.IgnoreCase))
            return Array.Empty<ResultadoPesquisaWeb>();

        return await PesquisarWikipediaAsync(consultaLimpa, limiteReal, cancellationToken);
    }

    private static IReadOnlyList<ResultadoPesquisaWeb> AplicarRestricaoDominioSolicitado(
        string consulta,
        IEnumerable<ResultadoPesquisaWeb> resultados)
    {
        var correspondencia = Regex.Match(
            consulta,
            @"(?:^|\s)site:(?<dominio>[^\s]+)",
            RegexOptions.IgnoreCase);

        if (!correspondencia.Success)
            return resultados.ToList();

        var dominioSolicitado = correspondencia.Groups["dominio"].Value
            .Trim()
            .TrimEnd('/')
            .ToLowerInvariant();

        return resultados
            .Where(resultado =>
            {
                if (!Uri.TryCreate(resultado.Url, UriKind.Absolute, out var uri))
                    return false;

                var host = uri.Host.ToLowerInvariant();
                return host.Equals(dominioSolicitado, StringComparison.OrdinalIgnoreCase) ||
                       host.EndsWith("." + dominioSolicitado, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    private static void AdicionarResultadosDiversificados(
        List<ResultadoPesquisaWeb> destino,
        IEnumerable<ResultadoPesquisaWeb> novos,
        int limite)
    {
        foreach (var resultado in novos)
        {
            if (destino.Count >= limite ||
                string.IsNullOrWhiteSpace(resultado.Url) ||
                destino.Any(x => x.Url.Equals(resultado.Url, StringComparison.OrdinalIgnoreCase)))
                continue;

            var dominio = ObterDominioFonte(resultado.Url);
            if (string.IsNullOrWhiteSpace(dominio))
                continue;

            var quantidadeDominio = destino.Count(x =>
                ObterDominioFonte(x.Url).Equals(dominio, StringComparison.OrdinalIgnoreCase));

            var ehWikipedia = dominio.Equals("wikipedia.org", StringComparison.OrdinalIgnoreCase);
            if ((ehWikipedia && quantidadeDominio >= 1) || (!ehWikipedia && quantidadeDominio >= 2))
                continue;

            destino.Add(resultado);
        }
    }

    private static bool TemDiversidadeSuficiente(
        IReadOnlyCollection<ResultadoPesquisaWeb> resultados,
        int limite)
    {
        var dominios = resultados
            .Select(x => ObterDominioFonte(x.Url))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return resultados.Count >= Math.Min(limite, 7) &&
               dominios >= Math.Min(limite, 4);
    }

    private static string ObterDominioFonte(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return string.Empty;

        var host = uri.Host.Trim().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        if (host.EndsWith(".wikipedia.org", StringComparison.Ordinal) ||
            host.Equals("wikipedia.org", StringComparison.Ordinal))
            return "wikipedia.org";

        return host;
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FetchTimeout);
        return await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FetchTimeout);
        return await _httpClient.SendAsync(request, completionOption, cts.Token).ConfigureAwait(false);
    }

    private static bool IsDotNetQuery(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return false;

        return Regex.IsMatch(
            consulta,
            @"(?i)(?<![\p{L}\p{N}])(?:\.net|dotnet|c#|f#|vb\.net|linq|entity\s+framework|ef\s+core|asp\.net|blazor|maui|wpf|avalonia)(?![\p{L}\p{N}])");
    }
    private static string NormalizarConsulta(string consulta)
    {
        var resultado = consulta.Trim();
        resultado = Regex.Replace(
            resultado,
            @"^(?:(?:ol[áa])\s*[,!:;-]?\s+)?kayron\b[\s,.:;!?—-]*",
            string.Empty,
            RegexOptions.IgnoreCase);
        resultado = Regex.Replace(resultado, @"\bvenesuela\b", "Venezuela", RegexOptions.IgnoreCase);
        var prefixos = new[]
        {
            "informações atuais sobre ",
            "informacoes atuais sobre ",
            "informação atual sobre ",
            "informacao atual sobre ",
            "informações sobre ",
            "informacoes sobre ",
            "notícias sobre ",
            "noticias sobre ",
            "dados sobre ",
            "pesquise na internet: ",
            "pesquise na internet ",
            "pesquise na web: ",
            "pesquise na web ",
            "pesquisar na internet: ",
            "pesquisar na internet ",
            "pesquisar na web: ",
            "pesquisar na web ",
            "pesquisa na internet ",
            "pesquisa na web ",
            "procure na internet ",
            "procure na web ",
            "busque na internet ",
            "busque na web ",
            "pesquise ",
            "pesquisar ",
            "pesquisa ",
            "procure ",
            "busque "
        };

        bool prefixoRemovido;
        do
        {
            prefixoRemovido = false;
            foreach (var prefixo in prefixos)
            {
                if (resultado.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                {
                    resultado = resultado[prefixo.Length..].Trim();
                    prefixoRemovido = true;
                    break;
                }
            }
        } while (prefixoRemovido);

        resultado = resultado.Trim().TrimEnd('.', '?', '!', ' ');
        var sufixos = new[]
        {
            " e apresente as fontes consultadas",
            " e apresente as fontes",
            " e inclua as fontes consultadas",
            " e mostre as fontes consultadas",
            " e informe as fontes consultadas",
            " e cite as fontes consultadas",
            " e cite fontes consultadas",
            " e cite fontes",
            " cite as fontes consultadas",
            " cite fontes consultadas",
            " cite fontes"
        };

        foreach (var sufixo in sufixos)
        {
            if (resultado.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase))
            {
                resultado = resultado[..^sufixo.Length].Trim();
                break;
            }
        }

        return resultado;
    }


    private static bool TemRelevanciaBasica(
        IReadOnlyList<ResultadoPesquisaWeb> resultados,
        string consulta)
    {
        var termos = consulta
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 4)
            .Select(NormalizarTermo)
            .Where(x => x.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (termos.Length == 0)
            return resultados.Count > 0;

        var termosGenericos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "principais", "principal", "atual", "atuais", "atualmente", "hoje", "sobre",
            "quem", "qual", "quais", "como", "quando", "onde", "porque", "porquê",
            "informacao", "informacoes", "encontrada", "encontradas",
            "fontes", "fonte", "pesquisa", "pesquisar", "pesquise",
            "apresente", "apresentar", "consultada", "consultadas", "consultado", "consultados",
            "internet", "web", "dados", "recentes"
        };

        var termosEspecificos = termos
            .Where(termo => !termosGenericos.Contains(termo))
            .ToArray();

        return resultados.Any(r =>
        {
            var texto = new string(
                $"{r.Titulo} {r.Trecho} {r.Url}"
                    .ToLowerInvariant()
                    .Normalize(System.Text.NormalizationForm.FormD)
                    .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    .ToArray());

            var palavrasTexto = Regex.Split(texto, @"[^\p{L}\p{Nd}]+");
            var acertosEspecificos = termosEspecificos.Count(termo =>
                texto.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                (termo.Length >= 6 && palavrasTexto.Any(palavra =>
                    palavra.Length >= 6 && DistanciaEdicaoMaximaUm(termo, palavra))));

            // Exija pelo menos dois termos específicos, sem exigir que o título
            // ou o trecho repita quase toda a consulta. Isso evita descartar fontes
            // relevantes que usam sinônimos ou cobrem apenas parte da pergunta.
            var minimoAcertos = Math.Min(2, termosEspecificos.Length);
            return acertosEspecificos >= minimoAcertos;
        });
    }

    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarGoogleAsync(
        string consulta,
        int limite,
        CancellationToken cancellationToken)
    {
        var url =
            "https://www.google.com/search?q=" + Uri.EscapeDataString(consulta) +
            "&num=" + limite + "&hl=pt-BR&gl=BR";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/142 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");

        using var response = await SendWithTimeoutAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google retornou HTTP {Status}.", (int)response.StatusCode);
            return Array.Empty<ResultadoPesquisaWeb>();
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtrairResultados(html, limite);
    }

    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> EnriquecerResultadosAsync(
        IReadOnlyList<ResultadoPesquisaWeb> resultados,
        string consulta,
        CancellationToken cancellationToken)
    {
        if (resultados.Count == 0)
            return resultados;

        using var limiteCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limiteCts.CancelAfter(FetchTimeout);

        var tarefas = resultados
            .Take(8)
            .Select(r => EnriquecerResultadoAsync(r, consulta, limiteCts.Token))
            .ToArray();

        var enriquecidos = await Task.WhenAll(tarefas);
        return enriquecidos
            .Where(r => !string.IsNullOrWhiteSpace(r.Trecho))
            .Take(resultados.Count)
            .ToArray();
    }

    private async Task<ResultadoPesquisaWeb> EnriquecerResultadoAsync(
        ResultadoPesquisaWeb resultado,
        string consulta,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, resultado.Url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/142 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,text/plain;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return resultado;

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
                return resultado;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var texto = await reader.ReadToEndAsync(cancellationToken);
            var conteudo = ExtrairConteudoPrincipal(texto);
            if (conteudo.Length < 80)
                return resultado;

            var prefixo = ConstruirTrechoRelevante(conteudo, consulta);
            return new ResultadoPesquisaWeb(resultado.Titulo, resultado.Url, prefixo);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "NÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o foi possÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­vel obter conteÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºdo de {Url}.", resultado.Url);
            return resultado;
        }
    }

    private static string ExtrairConteudoPrincipal(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            return string.Empty;

        if (!conteudo.Contains('<'))
            return LimitarConteudo(conteudo);

        conteudo = Regex.Replace(conteudo, "<script\\b[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        conteudo = Regex.Replace(conteudo, "<style\\b[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        conteudo = Regex.Replace(conteudo, "<noscript\\b[^>]*>.*?</noscript>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        conteudo = Regex.Replace(conteudo, "<svg\\b[^>]*>.*?</svg>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        conteudo = Regex.Replace(conteudo, "</?(?:nav|footer|header|aside|form)\\b[^>]*>.*?</(?:nav|footer|header|aside|form)>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        conteudo = Regex.Replace(conteudo, "<[^>]+>", " ");
        conteudo = WebUtility.HtmlDecode(conteudo);
        conteudo = Regex.Replace(conteudo, @"\\s+", " ").Trim();
        return LimitarConteudo(conteudo);
    }

    private static string ConstruirTrechoRelevante(string conteudo, string consulta)
    {
        var termos = consulta.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizarTermo)
            .Where(x => x.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (termos.Length == 0 || conteudo.Length <= 3500)
            return conteudo;

        var melhor = 0;
        var melhorPontuacao = -1;
        var janela = Math.Min(3500, conteudo.Length);
        for (var inicio = 0; inicio <= conteudo.Length - 120; inicio += 450)
        {
            var fim = Math.Min(inicio + janela, conteudo.Length);
            var trecho = conteudo[inicio..fim];
            var pontos = termos.Count(t => trecho.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (pontos > melhorPontuacao)
            {
                melhorPontuacao = pontos;
                melhor = inicio;
            }
        }

        var inicioFinal = Math.Max(0, melhor - 400);
        return conteudo[inicioFinal..Math.Min(conteudo.Length, inicioFinal + MaxConteudoPagina)].Trim();
    }

    private static string LimitarConteudo(string valor)
    {
        valor = valor.Trim();
        return valor.Length <= MaxConteudoPagina ? valor : valor[..MaxConteudoPagina];
    }
    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarTavilyAsync(
        string consulta,
        int limite,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            api_key = apiKey,
            query = consulta,
            max_results = limite,
            search_depth = "advanced",
            include_answer = false,
            include_raw_content = true
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search");
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        using var response = await SendWithTimeoutAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Tavily retornou HTTP {Status}; usando prÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ximo provedor.", (int)response.StatusCode);
            return Array.Empty<ResultadoPesquisaWeb>();
        }

        return ExtrairResultadosTavily(
            await response.Content.ReadAsStringAsync(cancellationToken), limite);
    }

    private static IReadOnlyList<ResultadoPesquisaWeb> ExtrairResultadosTavily(string json, int limite)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (!documento.RootElement.TryGetProperty("results", out var itens) || itens.ValueKind != JsonValueKind.Array)
                return Array.Empty<ResultadoPesquisaWeb>();

            var resultados = new List<ResultadoPesquisaWeb>();
            foreach (var item in itens.EnumerateArray())
            {
                var titulo = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                var trecho = item.TryGetProperty("raw_content", out var raw) ? raw.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(trecho) && item.TryGetProperty("content", out var content))
                    trecho = content.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(titulo) && !string.IsNullOrWhiteSpace(url))
                    resultados.Add(new ResultadoPesquisaWeb(titulo.Trim(), url.Trim(), LimitarConteudo(trecho)));
            }
            return resultados.Take(limite).ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<ResultadoPesquisaWeb>();
        }
    }

    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarBraveAsync(
        string consulta,
        int limite,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = "https://api.search.brave.com/res/v1/llm/context?q=" +
                  Uri.EscapeDataString(consulta) +
                  "&count=" + limite +
                  "&country=BR&search_lang=pt-br";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("X-Subscription-Token", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Brave retornou HTTP {Status}; usando prÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ximo provedor.", (int)response.StatusCode);
            return Array.Empty<ResultadoPesquisaWeb>();
        }

        return ExtrairResultadosBrave(
            await response.Content.ReadAsStringAsync(cancellationToken), limite);
    }

    private static IReadOnlyList<ResultadoPesquisaWeb> ExtrairResultadosBrave(string json, int limite)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            var resultados = new List<ResultadoPesquisaWeb>();
            if (!documento.RootElement.TryGetProperty("grounding", out var grounding) ||
                !grounding.TryGetProperty("generic", out var generic) ||
                generic.ValueKind != JsonValueKind.Array)
                return Array.Empty<ResultadoPesquisaWeb>();

            foreach (var item in generic.EnumerateArray())
            {
                var titulo = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                var partes = new List<string>();
                if (item.TryGetProperty("snippets", out var snippets) && snippets.ValueKind == JsonValueKind.Array)
                    partes.AddRange(snippets.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
                var trecho = string.Join("\n", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(titulo) && !string.IsNullOrWhiteSpace(url))
                    resultados.Add(new ResultadoPesquisaWeb(titulo.Trim(), url.Trim(), LimitarConteudo(trecho)));
            }
            return resultados.Take(limite).ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<ResultadoPesquisaWeb>();
        }
    }

    private static IReadOnlyList<ResultadoPesquisaWeb> ExtrairResultados(string html, int limite)
    {
        var resultados = new List<ResultadoPesquisaWeb>();

        // O Google altera classes dos containers com frequÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªncia. A relaÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â§ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o estrutural
        // <a href="..."><h3>tÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­tulo</h3></a> ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â© mais estÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡vel que MjjYud/tF2Cxc.
        var padrao = "<a[^>]+href=\"(?<url>https?://[^\"]+)\"[^>]*>\\s*<h3[^>]*>(?<titulo>.*?)</h3>";
        foreach (Match match in Regex.Matches(html, padrao, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var url = NormalizarUrl(match.Groups["url"].Value);
            var titulo = LimparHtml(match.Groups["titulo"].Value);
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(titulo))
                continue;
            if (EhUrlGoogle(url) || resultados.Any(x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                continue;

            var inicio = Math.Max(0, match.Index - 200);
            var fim = Math.Min(html.Length, match.Index + match.Length + 2500);
            var bloco = html[inicio..fim];
            var trechoMatch = Regex.Match(
                bloco,
                "<div[^>]+class=\"[^\"]*(?:VwiC3b|yXK7lf|VwiC3b[^\"]*)[^\"]*\"[^>]*>(?<trecho>.*?)</div>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var trecho = trechoMatch.Success
                ? LimparHtml(trechoMatch.Groups["trecho"].Value)
                : LimparHtml(bloco);

            resultados.Add(new ResultadoPesquisaWeb(titulo, url, trecho));
            if (resultados.Count >= limite)
                break;
        }

        return resultados.Take(limite).ToArray();
    }

    private static bool EhUrlGoogle(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.Host.Contains("google.", StringComparison.OrdinalIgnoreCase);
    }
    private static string NormalizarUrl(string valor)
    {
        var url = WebUtility.HtmlDecode(valor);
        if (url.StartsWith("/url?q=", StringComparison.OrdinalIgnoreCase))
            url = url[7..];
        var encoded = Regex.Match(url, "[?&]u=a1([^&]+)");
        if (encoded.Success)
        {
            try
            {
                var base64 = encoded.Groups[1].Value.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Trim();
            }
            catch
            {
            }
        }
        return WebUtility.UrlDecode(url).Trim();
    }

    private static string ConstruirConsultaWikipedia(string consulta)
    {
        var ignorar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "o", "as", "os", "e", "de", "do", "da", "dos", "das",
            "em", "no", "na", "nos", "nas", "por", "para", "com",
            "qual", "quais", "que", "como", "quando", "onde", "quem",
            "é", "e", "um", "uma", "uns", "umas", "sobre", "me", "se"
        };

        var termos = consulta
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray();

        var texto = new string(termos).ToLowerInvariant();

        var palavras = texto
            .Split(
                new[] { ' ', '\t', '\r', '\n', ',', '.', ':', ';', '?', '!', '-', '_', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 3 && !ignorar.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        return palavras.Length > 0
            ? string.Join(' ', palavras)
            : consulta.Trim();
    }

    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarWikipediaAsync(
        string consulta,
        int limite,
        CancellationToken cancellationToken)
    {
        try
        {
            var consultaWikipedia = ConstruirConsultaWikipedia(consulta);

            var url =
                "https://pt.wikipedia.org/w/api.php?action=query&list=search" +
                "&format=json&utf8=1&srlimit=" +
                limite +
                "&srsearch=" +
                Uri.EscapeDataString(consultaWikipedia);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "KAYRON/1.0");
            using var response = await SendWithTimeoutAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return Array.Empty<ResultadoPesquisaWeb>();

            using var documento =
                JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));

            if (!documento.RootElement.TryGetProperty("query", out var consultaElemento) ||
                !consultaElemento.TryGetProperty("search", out var resultadosElemento) ||
                resultadosElemento.ValueKind != JsonValueKind.Array)
                return Array.Empty<ResultadoPesquisaWeb>();

            var resultados = new List<ResultadoPesquisaWeb>();

            foreach (var item in resultadosElemento.EnumerateArray())
            {
                var titulo =
                    item.TryGetProperty("title", out var tituloElemento)
                        ? tituloElemento.GetString() ?? string.Empty
                        : string.Empty;

                var trecho =
                    item.TryGetProperty("snippet", out var trechoElemento)
                        ? LimparHtml(trechoElemento.GetString() ?? string.Empty)
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(titulo))
                    continue;

                var urlPagina =
                    "https://pt.wikipedia.org/wiki/" +
                    Uri.EscapeDataString(titulo.Replace(' ', '_'));

                resultados.Add(
                    new ResultadoPesquisaWeb(
                        titulo,
                        urlPagina,
                        trecho));

                if (resultados.Count >= limite)
                    break;
            }

            return TemRelevanciaBasica(resultados, consulta)
                ? resultados
                : Array.Empty<ResultadoPesquisaWeb>();
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            JsonException or
            TaskCanceledException)
        {
            _logger.LogDebug(ex, "Fallback da Wikipedia indisponível.");
            return Array.Empty<ResultadoPesquisaWeb>();
        }
    }

    private static string LimparHtml(string valor)
    {
        var texto = Regex.Replace(valor, "<[^>]+>", " ");
        texto = WebUtility.HtmlDecode(texto);
        texto = texto.Replace("**", string.Empty, StringComparison.Ordinal);
        return Regex.Replace(texto, @"\s+", " ").Trim();
    }

    private static bool DistanciaEdicaoMaximaUm(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
            return false;

        var i = 0;
        var j = 0;
        var diferencas = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[j]))
            {
                i++;
                j++;
                continue;
            }

            if (++diferencas > 1)
                return false;

            if (a.Length > b.Length)
                i++;
            else if (b.Length > a.Length)
                j++;
            else
            {
                i++;
                j++;
            }
        }

        if (i < a.Length || j < b.Length)
            diferencas++;

        return diferencas <= 1;
    }

    private static string NormalizarTermo(string termo)
    {
        var normalizado = termo.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        normalizado = new string(normalizado
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Regex.Replace(normalizado, @"[^\p{L}\p{Nd}]", "");
    }
}
