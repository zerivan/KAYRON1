using KAYRON.Core;
using KAYRON.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KAYRON.Agent;

public class Class2
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: false);

        var caminhoMemoria =
            builder.Configuration["KAYRON:Memoria:Caminho"];

        if (string.IsNullOrWhiteSpace(caminhoMemoria))
        {
            throw new InvalidOperationException(
                "A configuração KAYRON:Memoria:Caminho não foi definida.");
        }

        builder.Services.AddSingleton<IMemoria, Memoria>();
        builder.Services.AddSingleton<IEmbeddingMemoria, EmbeddingMemoriaLocal>();
        builder.Services.Configure<PostgresMemoriaOptions>(builder.Configuration.GetSection(PostgresMemoriaOptions.SectionName));
        builder.Services.AddSingleton<MemoriaPostgres>();
        builder.Services.AddSingleton<MemoriaSemantica>();

        builder.Services.AddSingleton<MemoriaAprendidaPersistente>(
            serviceProvider =>
                new MemoriaAprendidaPersistente(
                    caminhoMemoria,
                    serviceProvider.GetRequiredService<
                        ILogger<MemoriaAprendidaPersistente>>()));

        builder.Services.AddSingleton<IMemoriaAprendida>(
            serviceProvider =>
                new KAYRON.Engine.MemoriaAprendida(
                    serviceProvider.GetRequiredService<
                        MemoriaAprendidaPersistente>(),
                    serviceProvider.GetRequiredService<MemoriaPostgres>()));

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
        builder.Services.AddSingleton<PesquisaWeb>();
        builder.Services.AddSingleton<DeepSearch>();
        builder.Services.AddSingleton<GestorConhecimento>();
        builder.Services.AddSingleton<IModeloInteligencia>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>()
                .Value;

            if (options.Provedor.Equals("IBM", StringComparison.OrdinalIgnoreCase) ||
                options.Provedor.Equals("Watsonx", StringComparison.OrdinalIgnoreCase))
            {
                return new ModeloInteligenciaWatsonx(
                    serviceProvider.GetRequiredService<HttpClient>(),
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>(),
                    serviceProvider.GetRequiredService<ILogger<ModeloInteligenciaWatsonx>>());
            }

            if (options.Provedor.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                return new ModeloInteligenciaGemini(
                    serviceProvider.GetRequiredService<HttpClient>(),
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>(),
                    serviceProvider.GetRequiredService<ILogger<ModeloInteligenciaGemini>>());
            }

            return new ModeloInteligenciaLocal(
                serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>(),
                serviceProvider.GetRequiredService<ILogger<ModeloInteligenciaLocal>>());
        });

        builder.Services.AddSingleton<IDecisor, Decisor>();
        builder.Services.AddSingleton<IProcessador, Processador>();
        builder.Services.AddSingleton<ComandoAprender>();
        builder.Services.AddSingleton<IOrquestrador, Orquestrador>();
        builder.Services.AddSingleton<Aplicacao>();

        using var host = builder.Build();

        host.Services.RegistrarFerramentas();

        var dispatcher =
            host.Services.GetRequiredService<DispatcherComandos>();

        dispatcher.Registrar(
            host.Services.GetRequiredService<ComandoAprender>());

        var aplicacao =
            host.Services.GetRequiredService<Aplicacao>();

        await aplicacao.ExecutarAsync();
    }
}

