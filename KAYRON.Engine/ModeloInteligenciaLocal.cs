using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public class ModeloInteligenciaLocal : IModeloInteligencia
{
    private readonly ModeloInteligenciaOptions _options;
    private readonly ILogger<ModeloInteligenciaLocal> _logger;
    private readonly HttpClient _httpClient = new();
    private readonly DeepSearch _deepSearch;

    public ModeloInteligenciaLocal(
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<ModeloInteligenciaLocal> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;

        var web = new PesquisaWeb(
            _httpClient,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PesquisaWeb>.Instance);

        _deepSearch = new DeepSearch(
            _httpClient,
            web,
            new EmbeddingMemoriaLocal(),
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DeepSearch>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ModeloInteligenciaHuggingFace>.Instance);

        _ = _deepSearch;
    }

    public async Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.Equals(
                contexto.Obter("intencao_detectada"),
                "internet:pesquisar",
                StringComparison.OrdinalIgnoreCase))
        {
            return await _deepSearch.GerarAsync(
                instrucao,
                contexto,
                cancellationToken);
        }

        var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
        if (!string.IsNullOrWhiteSpace(hfToken) ||
            _options.Provedor.Equals("HuggingFace", StringComparison.OrdinalIgnoreCase) ||
            _options.Provedor.Equals("HF", StringComparison.OrdinalIgnoreCase))
        {
            var modelo = new ModeloInteligenciaHuggingFace(
                _httpClient,
                Options.Create(_options),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ModeloInteligenciaHuggingFace>.Instance);

            return await modelo.GerarAsync(instrucao, contexto, cancellationToken);
        }

        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(geminiKey) ||
            _options.Provedor.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var modelo = new ModeloInteligenciaGemini(
                _httpClient,
                Options.Create(_options),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ModeloInteligenciaGemini>.Instance);

            return await modelo.GerarAsync(instrucao, contexto, cancellationToken);
        }

        var conteudo = instrucao.Conteudo.Trim();
        _logger.LogWarning(
            "Nenhum provedor de IA configurado. Use HF_TOKEN ou GEMINI_API_KEY.");

        return new Resposta
        {
            Conteudo =
                $"KAYRON est\u00E1 pronto, mas o provedor de IA ainda n\u00E3o foi configurado. " +
                $"Configure HF_TOKEN para habilitar o modelo principal. Entrada recebida: {conteudo}"
        };
    }
}
