using System.Net.Http.Json;
using System.Text.Json;
using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public sealed class ModeloInteligenciaGemini : IModeloInteligencia
{
    private readonly HttpClient _httpClient;
    private readonly ModeloInteligenciaOptions _options;
    private readonly ILogger<ModeloInteligenciaGemini> _logger;

    public ModeloInteligenciaGemini(
        HttpClient httpClient,
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<ModeloInteligenciaGemini> logger)
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

        var apiKey = ObterApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "A chave Gemini não foi configurada. Defina GEMINI_API_KEY no ambiente.");
        }

        var modelo = string.IsNullOrWhiteSpace(_options.Modelo)
            ? "gemini-3.8-flash"
            : _options.Modelo.Trim();

        var prompt = ConstruirPrompt(instrucao, contexto);

        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = "Você é o núcleo de inteligência do KAYRON, um agente local de software. Responda em português, use o contexto fornecido, não invente ações executadas e seja preciso. CONTRATO DE CONFIABILIDADE: nunca apresente uma informação como fato quando não houver base suficiente. Não chute, não complete lacunas com suposições e não transforme hipótese em certeza. Quando uma resposta anterior for contestada como errada, considere a resposta contestada inválida até nova verificação, use a evidência pesquisada no turno e responda somente com o que essa evidência sustenta. Se a evidência não for suficiente para confirmar a resposta, declare a limitação em vez de inventar. Uma correção verificada deve substituir o conhecimento anterior correspondente, nunca ser acumulada como uma segunda versão contraditória. Quando a instrução começar com [KAYRON_DECISAO], ela é uma solicitação de planejamento: retorne SOMENTE o JSON solicitado, sem markdown e sem explicação. Quando houver resultado de ferramenta, use-o para decidir a próxima ação, mas não informe ao usuário o status de ações pendentes ou concluídas, a menos que isso seja explicitamente solicitado." }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = _options.Temperatura
            }
        };

        HttpResponseMessage? response = null;
        string body = string.Empty;

        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            response?.Dispose();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent");

            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(payload);

            response = await _httpClient.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode ||
                ((int)response.StatusCode != 408 &&
                 (int)response.StatusCode != 429 &&
                 ((int)response.StatusCode < 500 || (int)response.StatusCode > 599)))
            {
                break;
            }

            if (tentativa < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, tentativa)), cancellationToken);
            }
        }
        using (response)

        if (!response!.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini retornou HTTP {Status}: {Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"Falha na API Gemini: HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var texto = ExtrairTexto(document);

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new InvalidOperationException(
                "A API Gemini não retornou texto utilizável.");
        }

        return new Resposta
        {
            Conteudo = texto.Trim()
        };
    }
    private string ObterApiKey()
    {
        return Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? _options.ApiKey;
    }

    private static string ConstruirPrompt(
        Instrucao instrucao,
        IContexto contexto)
    {
        var entrada = contexto.Obter("entrada_original")
            ?? instrucao.Conteudo;

        var contextoConversacional = contexto.Obter("contexto_conversacional");
        var memoria = contexto.Obter("memoria:aprendida_relevante");
        var valorMemoria = string.IsNullOrWhiteSpace(memoria)
            ? null
            : contexto.Obter($"memoria:aprendida:{memoria}");
        var resultado = contexto.Obter("ciclo:ultimo_resultado");
        var conhecimentoAdquirido = contexto.Obter("conhecimento:adquirido");
        var intencao = contexto.Obter("intencao_detectada");

        return $"""
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

CONHECIMENTO ADQUIRIDO PELO KAYRON:
{(string.IsNullOrWhiteSpace(conhecimentoAdquirido) ? "nenhum" : conhecimentoAdquirido)}

TAREFA:
Responda diretamente ao usuário considerando os dados acima. Para perguntas informativas, entregue somente a resposta solicitada. Não acrescente frases sobre ações pendentes, ações concluídas, próximas ações ou estado interno do KAYRON, salvo se o usuário pedir esse tipo de informação. Não diga que executou uma ação se ela não aparece no resultado.
""";
    }

    private static string? ExtrairTexto(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("candidates", out var candidates))
            return null;

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts))
                continue;

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                    return text.GetString();
            }
        }

        return null;
    }
}
