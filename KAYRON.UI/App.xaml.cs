using System.Net.Http;
using System.Windows;
using KAYRON.Core;
using KAYRON.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KAYRON.UI;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder(e.Args);
        // Mantém os provedores padrão de log para que erros do agente sejam observáveis.
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        var caminhoMemoria = builder.Configuration["KAYRON:Memoria:Caminho"];
        if (string.IsNullOrWhiteSpace(caminhoMemoria))
            throw new InvalidOperationException("A configuração de memória do KAYRON não foi definida.");

        builder.Services.AddSingleton<IMemoria, Memoria>();
        builder.Services.AddSingleton<IEmbeddingMemoria, EmbeddingMemoriaLocal>();
        builder.Services.Configure<PostgresMemoriaOptions>(builder.Configuration.GetSection(PostgresMemoriaOptions.SectionName));
        builder.Services.AddSingleton<MemoriaPostgres>();
        builder.Services.AddSingleton<MemoriaSemantica>();
        builder.Services.AddSingleton<MemoriaAprendidaPersistente>(sp =>
            new MemoriaAprendidaPersistente(caminhoMemoria, sp.GetRequiredService<ILogger<MemoriaAprendidaPersistente>>()));
        builder.Services.AddSingleton<IMemoriaAprendida>(sp => new KAYRON.Engine.MemoriaAprendida(sp.GetRequiredService<MemoriaAprendidaPersistente>(), sp.GetRequiredService<MemoriaPostgres>()));
        builder.Services.AddSingleton<Interpretador>();
        builder.Services.AddSingleton<DispatcherComandos>();
        builder.Services.AddFerramentasKAYRON();
        builder.Services.AddSingleton<PesquisaWeb>();
        builder.Services.AddSingleton<DeepSearch>();
        builder.Services.AddSingleton<GestorConhecimento>();
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
        builder.Services.Configure<ModeloInteligenciaOptions>(builder.Configuration.GetSection(ModeloInteligenciaOptions.SectionName));
        builder.Services.AddSingleton<HttpClient>();
        builder.Services.AddSingleton<IModeloInteligencia>(sp => CriarModelo(sp));
        builder.Services.AddSingleton<IDecisor, Decisor>();
        builder.Services.AddSingleton<IProcessador, Processador>();
        builder.Services.AddSingleton<ComandoAprender>();
        builder.Services.AddSingleton<IOrquestrador, Orquestrador>();

        _host = builder.Build();
        _host.Services.RegistrarFerramentas();
        _host.Services.GetRequiredService<DispatcherComandos>().Registrar(_host.Services.GetRequiredService<ComandoAprender>());
        Services = _host.Services;
        MainWindow = new MainWindow(_host.Services.GetRequiredService<GerenciadorConversa>());
        MainWindow.Show();
    }

    private static IModeloInteligencia CriarModelo(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>().Value;
        var http = sp.GetRequiredService<HttpClient>();
        if (options.Provedor.Equals("IBM", StringComparison.OrdinalIgnoreCase) || options.Provedor.Equals("Watsonx", StringComparison.OrdinalIgnoreCase))
            return new ModeloInteligenciaWatsonx(http, sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>(), sp.GetRequiredService<ILogger<ModeloInteligenciaWatsonx>>());
        if (options.Provedor.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            return new ModeloInteligenciaGemini(http, sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>(), sp.GetRequiredService<ILogger<ModeloInteligenciaGemini>>());
        return new ModeloInteligenciaLocal(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>(), sp.GetRequiredService<ILogger<ModeloInteligenciaLocal>>());
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
        base.OnExit(e);
    }
}