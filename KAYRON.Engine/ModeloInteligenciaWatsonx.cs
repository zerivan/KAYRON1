using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public sealed class ModeloInteligenciaWatsonx : IModeloInteligencia
{
    private readonly HttpClient _httpClient;
    private readonly ModeloInteligenciaOptions _options;
    private readonly ILogger<ModeloInteligenciaWatsonx> _logger;

    public ModeloInteligenciaWatsonx(
        HttpClient httpClient,
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<ModeloInteligenciaWatsonx> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        var apiKey = Environment.GetEnvironmentVariable("IBM_CLOUD_API_KEY")
            ?? _options.ApiKey;
        var projectId = Environment.GetEnvironmentVariable("WATSONX_PROJECT_ID")
            ?? _options.ProjectId;
        var baseUrl = Environment.GetEnvironmentVariable("WATSONX_URL")
            ?? _options.Url;

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("A chave IBM Cloud não foi configurada. Defina IBM_CLOUD_API_KEY.");

        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("O projeto watsonx.ai não foi configurado. Defina WATSONX_PROJECT_ID.");

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("A URL do watsonx.ai não foi configurada. Defina WATSONX_URL.");

        var token = await ObterTokenAsync(apiKey, cancellationToken);
        var modelo = string.IsNullOrWhiteSpace(_options.Modelo)
            ? "ibm/granite-4-h-small"
            : _options.Modelo.Trim();
        var prompt = ConstruirPrompt(instrucao, contexto);
        var url = $"{baseUrl.TrimEnd('/')}/ml/v1/text/generation?version=2024-05-01";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model_id = modelo,
                input = prompt,
                project_id = projectId,
                parameters = new
                {
                    temperature = _options.Temperatura,
                    max_new_tokens = 4096,
                    stop_sequences = Array.Empty<string>()
                }
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("watsonx.ai retornou HTTP {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Falha na API IBM watsonx.ai: HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var texto = document.RootElement
            .GetProperty("results")[0]
            .GetProperty("generated_text")
            .GetString();

        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("A API IBM watsonx.ai não retornou texto utilizável.");

        return new Resposta { Conteudo = texto.Trim() };
    }

    private async Task<string> ObterTokenAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://iam.cloud.ibm.com/identity/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ibm:params:oauth:grant-type:apikey",
            ["apikey"] = apiKey
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha na autenticação IBM: HTTP {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("A IBM não retornou um token de acesso.");
    }

    private static string ConstruirPrompt(Instrucao instrucao, IContexto contexto)
    {
        var entrada = contexto.Obter("entrada_original") ?? instrucao.Conteudo;
        var contextoConversacional = contexto.Obter("contexto_conversacional");
        var memoria = contexto.Obter("memoria:aprendida_relevante");
        var valorMemoria = string.IsNullOrWhiteSpace(memoria)
            ? null
            : contexto.Obter($"memoria:aprendida:{memoria}");
        var resultado = contexto.Obter("ciclo:ultimo_resultado");
        var intencao = contexto.Obter("intencao_detectada");

        return $"""
Você é o núcleo de inteligência do KAYRON, um agente local de software.
Responda em português, seja preciso e não invente ações executadas.
Quando a instrução começar com [KAYRON_DECISAO], retorne SOMENTE o JSON solicitado, sem markdown.

ENTRADA DO USUÁRIO:
{entrada}

INTENÇÃO DETECTADA:
{intencao ?? "não identificada"}

CONTEXTO DA CONVERSA:
{contextoConversacional ?? "não disponível"}

MEMÓRIA RELEVANTE:
{(string.IsNullOrWhiteSpace(valorMemoria) ? "nenhuma" : valorMemoria)}

RESULTADO DA ÚLTIMA FERRAMENTA:
{(string.IsNullOrWhiteSpace(resultado) ? "nenhum" : resultado)}

TAREFA:
Responda considerando todos os dados acima. Se uma ferramenta produziu um resultado, use-o para decidir a próxima ação.
""";
    }
}
