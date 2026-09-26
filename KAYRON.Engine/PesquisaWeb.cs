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
            using var response = await _httpClient.SendAsync(request, cancellationToken);
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

        var braveKey = Environment.GetEnvironmentVariable("BRAVE_SEARCH_API_KEY");
        if (!string.IsNullOrWhiteSpace(braveKey))
        {
            var resultados = await PesquisarBraveAsync(consultaLimpa, limiteReal, braveKey, cancellationToken);
            resultados = await EnriquecerResultadosAsync(resultados, consultaLimpa, cancellationToken);
            if (resultados.Count > 0)
                return resultados;
        }

        var tavilyKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (!string.IsNullOrWhiteSpace(tavilyKey))
        {
            var resultados = await PesquisarTavilyAsync(consultaLimpa, limiteReal, tavilyKey, cancellationToken);
            resultados = await EnriquecerResultadosAsync(resultados, consultaLimpa, cancellationToken);
            if (resultados.Count > 0)
                return resultados;
        }

        // Bing ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â© usado antes do parser HTML do Google porque sua estrutura
        // atual estÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ retornando resultados de blocos de conteÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºdo nÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o relacionados
        // em algumas consultas em portuguÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªs. A relevÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ncia ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â© validada antes do enriquecimento.
        var bing = await PesquisarBingAsync(consultaLimpa, limiteReal, cancellationToken);
        if (bing.Count > 0)
            return await EnriquecerResultadosAsync(bing, consultaLimpa, cancellationToken);
        var google = await PesquisarGoogleAsync(consultaLimpa, limiteReal, cancellationToken);
        if (TemRelevanciaBasica(google, consultaLimpa))
            return await EnriquecerResultadosAsync(google, consultaLimpa, cancellationToken);

        var bingJina = await PesquisarBingViaJinaAsync(consultaLimpa, limiteReal, cancellationToken);
        return await EnriquecerResultadosAsync(bingJina, consultaLimpa, cancellationToken);
    }
    private static string NormalizarConsulta(string consulta)
    {
        var resultado = consulta.Trim();
        var prefixos = new[]
        {
            "pesquise na internet ",
            "pesquise na web ",
            "pesquisar na internet ",
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

        foreach (var prefixo in prefixos)
        {
            if (resultado.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                return resultado[prefixo.Length..].Trim();
        }

        return resultado;
    }


    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarBingAsync(
        string consulta,
        int limite,
        CancellationToken cancellationToken)
    {
        var url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(consulta) +
                  "&count=" + limite + "&setlang=pt-BR";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/142 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Array.Empty<ResultadoPesquisaWeb>();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtrairResultadosBing(html, limite);
    }

    private async Task<IReadOnlyList<ResultadoPesquisaWeb>> PesquisarBingViaJinaAsync(
        string consulta,
        int limite,
        CancellationToken cancellationToken)
    {
        var alvo = "https://www.bing.com/search?q=" + Uri.EscapeDataString(consulta);
        var url = "https://r.jina.ai/" + alvo;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<ResultadoPesquisaWeb>();

            var markdown = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtrairResultadosBingMarkdown(markdown, limite);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Fallback Jina/Bing indisponÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­vel.");
            return Array.Empty<ResultadoPesquisaWeb>();
        }
    }

    private static IReadOnlyList<ResultadoPesquisaWeb> ExtrairResultadosBingMarkdown(string markdown, int limite)
    {
        var resultados = new List<ResultadoPesquisaWeb>();
        var padrao = @"##\s*\[\*\*(?<titulo>.*?)\*\*\]\((?<url>https?://[^)]+)\)\s*(?<trecho>.*?)(?=\n\s*\d+\.\s+|\z)";
        foreach (Match match in Regex.Matches(markdown, padrao, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var titulo = LimparHtml(match.Groups["titulo"].Value);
            var url = NormalizarUrl(match.Groups["url"].Value);
            var trecho = LimparHtml(match.Groups["trecho"].Value);
            if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(url))
                continue;
            if (resultados.Any(x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                continue;

            resultados.Add(new ResultadoPesquisaWeb(titulo, url, LimitarConteudo(trecho)));
            if (resultados.Count >= limite)
                break;
        }

        return resultados.ToArray();
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

        return resultados.Any(r =>
        {
            var texto = $"{r.Titulo} {r.Trecho} {r.Url}".ToLowerInvariant();
            return termos.Any(texto.Contains);
        });
    }

    private static IReadOnlyList<ResultadoPesquisaWeb> ExtrairResultadosBing(string html, int limite)
    {
        var resultados = new List<ResultadoPesquisaWeb>();
        var padrao = @"<li[^>]+class=""[^""]*b_algo[^""]*""[^>]*>.*?<h2[^>]*>\s*<a[^>]+href=""(?<url>[^""]+)""[^>]*>(?<titulo>.*?)</a>.*?</h2>(?<resto>.*?)</li>";

        foreach (Match match in Regex.Matches(html, padrao, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var url = NormalizarUrl(match.Groups["url"].Value);
            var titulo = LimparHtml(match.Groups["titulo"].Value);
            var trecho = LimparHtml(match.Groups["resto"].Value);

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(titulo) ||
                url.Contains("bing.com", StringComparison.OrdinalIgnoreCase) ||
                resultados.Any(x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                continue;

            resultados.Add(new ResultadoPesquisaWeb(titulo, url, trecho));
            if (resultados.Count >= limite)
                break;
        }

        return resultados.ToArray();
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

        using var response = await _httpClient.SendAsync(request, cancellationToken);
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
        using var response = await _httpClient.SendAsync(request, cancellationToken);
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

    private static string LimparHtml(string valor)
    {
        var texto = Regex.Replace(valor, "<[^>]+>", " ");
        texto = WebUtility.HtmlDecode(texto);
        texto = texto.Replace("**", string.Empty, StringComparison.Ordinal);
        return Regex.Replace(texto, @"\s+", " ").Trim();
    }

    private static string NormalizarTermo(string termo)
    {
        return Regex.Replace(termo.ToLowerInvariant(), @"[^\p{L}\p{Nd}]", "");
    }
}
