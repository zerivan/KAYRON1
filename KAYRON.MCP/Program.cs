using KAYRON.Core;
using KAYRON.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

var caminhoMemoria = builder.Configuration["KAYRON:Memoria:Caminho"]
    ?? throw new InvalidOperationException("KAYRON:Memoria:Caminho não foi configurado.");

builder.Services.AddSingleton<IMemoria, Memoria>();
builder.Services.AddSingleton<IEmbeddingMemoria, EmbeddingMemoriaLocal>();
builder.Services.Configure<PostgresMemoriaOptions>(builder.Configuration.GetSection(PostgresMemoriaOptions.SectionName));
builder.Services.AddSingleton<MemoriaPostgres>();
builder.Services.AddSingleton<MemoriaSemantica>();
builder.Services.AddSingleton<MemoriaAprendidaPersistente>(sp =>
    new MemoriaAprendidaPersistente(
        caminhoMemoria,
        sp.GetRequiredService<ILogger<MemoriaAprendidaPersistente>>()));
builder.Services.AddSingleton<IMemoriaAprendida>(sp =>
    new KAYRON.Engine.MemoriaAprendida(
        sp.GetRequiredService<MemoriaAprendidaPersistente>(),
        sp.GetRequiredService<MemoriaPostgres>()));

builder.Services.AddSingleton<Interpretador>();
builder.Services.AddSingleton<DispatcherComandos>();
builder.Services.AddFerramentasKAYRON();
builder.Services.AddSingleton<IGerenciadorTarefas, GerenciadorTarefas>();
builder.Services.AddSingleton<IHistoricoConversa, HistoricoConversa>();
builder.Services.AddSingleton<IContextoConversacional, ContextoConversacional>();
builder.Services.AddSingleton<IResolvedorContexto, ResolvedorContexto>();
builder.Services.AddSingleton<IInterpretadorLinguagem, InterpretadorLinguagem>();
builder.Services.AddSingleton<GerenciadorConversa>();

builder.Services.AddSingleton<IPlanejador, Planejador>();
builder.Services.AddSingleton<IExecutorPlano, ExecutorPlano>();
builder.Services.AddSingleton<IContextoExecucao, GerenciadorContextoExecucao>();
builder.Services.AddSingleton<CatalogoCapacidades>();

builder.Services.Configure<ModeloInteligenciaOptions>(
    builder.Configuration.GetSection(ModeloInteligenciaOptions.SectionName));
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<IModeloInteligencia>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>().Value;
    var http = sp.GetRequiredService<HttpClient>();
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>();

    if (options.Provedor.Equals("IBM", StringComparison.OrdinalIgnoreCase) ||
        options.Provedor.Equals("Watsonx", StringComparison.OrdinalIgnoreCase))
        return new ModeloInteligenciaWatsonx(http, config, sp.GetRequiredService<ILogger<ModeloInteligenciaWatsonx>>());
    if (options.Provedor.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        return new ModeloInteligenciaGemini(http, config, sp.GetRequiredService<ILogger<ModeloInteligenciaGemini>>());

    return new ModeloInteligenciaLocal(
        config,
        sp.GetRequiredService<ILogger<ModeloInteligenciaLocal>>());
});

builder.Services.AddSingleton<IDecisor, Decisor>();
builder.Services.AddSingleton<IProcessador, Processador>();
builder.Services.AddSingleton<ComandoAprender>();
builder.Services.AddSingleton<IOrquestrador, Orquestrador>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

using var host = builder.Build();
host.Services.RegistrarFerramentas();

var dispatcher = host.Services.GetRequiredService<DispatcherComandos>();
dispatcher.Registrar(host.Services.GetRequiredService<ComandoAprender>());

await host.RunAsync();
