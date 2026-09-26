using System.Net.Http.Json;
using System.Text.Json;
using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public sealed class ModeloInteligenciaHuggingFace : IModeloInteligencia
{
    private readonly HttpClient _http;
    private readonly ModeloInteligenciaOptions _options;
    private readonly ILogger<ModeloInteligenciaHuggingFace> _logger;

    public ModeloInteligenciaHuggingFace(
        HttpClient http,
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<ModeloInteligenciaHuggingFace> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        var token = Environment.GetEnvironmentVariable("HF_TOKEN") ?? _options.ApiKey;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("HF_TOKEN não foi configurado.");

        var modelo = string.IsNullOrWhiteSpace(_options.Modelo)
            ? "openai/gpt-oss-120b:fastest"
            : _options.Modelo.Trim();

        var entrada = contexto.Obter("entrada_original") ?? instrucao.Conteudo;
        var conversa = contexto.Obter("mensagens_conversa_json");
        var memoria = contexto.Obter("memoria:aprendida_relevante");
        var contextoConversacional = contexto.Obter("contexto_conversacional");

        var system =
            "Você é o núcleo de inteligência do KAYRON. Responda em português quando o usuário escrever em português. " +
            "Seja preciso, técnico e útil. Não invente fatos, ferramentas executadas, fontes ou memórias. " +
            "Use o contexto e a memória fornecidos somente quando forem relevantes.";

        var prompt = $"""
ENTRADA ATUAL:
{entrada}

CONTEXTO CONVERSACIONAL:
{contextoConversacional ?? "não disponível"}

MEMÓRIA RELEVANTE:
{memoria ?? "nenhuma"}

HISTÓRICO:
{conversa ?? "não disponível"}

INSTRUÇÃO ATUAL:
{instrucao.Conteudo}
""";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://router.huggingface.co/v1/chat/completions");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            model = modelo,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperatura,
            stream = false
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Hugging Face retornou HTTP {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Falha no modelo Hugging Face: HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var texto = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("Hugging Face não retornou conteúdo utilizável.");

        return new Resposta { Conteudo = texto.Trim() };
    }
}
