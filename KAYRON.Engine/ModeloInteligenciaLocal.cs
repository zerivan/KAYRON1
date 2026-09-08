using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public class ModeloInteligenciaLocal : IModeloInteligencia
{
    private readonly ModeloInteligenciaOptions _options;
    private readonly ILogger<ModeloInteligenciaLocal> _logger;

    public ModeloInteligenciaLocal(
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<ModeloInteligenciaLocal> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        var conteudo = instrucao.Conteudo.Trim();

        _logger.LogDebug(
            "Modelo local processando instrução. Modelo: {Modelo}",
            _options.Modelo);

        return Task.FromResult(
            new Resposta
            {
                Conteudo = $"KAYRON identificou uma conversa: {conteudo}"
            });
    }
}
