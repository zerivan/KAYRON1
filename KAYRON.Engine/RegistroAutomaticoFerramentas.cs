using KAYRON.Core;
using Microsoft.Extensions.DependencyInjection;

namespace KAYRON.Engine;

public static class RegistroAutomaticoFerramentas
{
    public static IServiceCollection
        AddFerramentasKAYRON(
            this IServiceCollection services)
    {
        services.AddSingleton<CatalogoFerramentas>();

        services.AddSingleton<ICatalogoFerramentas>(
            provider =>
                provider.GetRequiredService<CatalogoFerramentas>());

        services.AddSingleton<ExecutorFerramentas>();
        services.AddSingleton<ITerminal, Terminal>();
        services.AddSingleton<IAutorizadorFerramentas, AutorizadorFerramentas>();
        services.AddSingleton<IConfirmadorOperacao, ConfirmadorOperacao>();
        services.AddSingleton<IDetectorProjeto, DetectorProjeto>();
        services.AddSingleton<IContextoProjeto, ContextoProjeto>();
        services.AddSingleton<IProcessadorConfirmacao, ProcessadorConfirmacao>();
        services.AddSingleton<IIdentificadorIdioma, IdentificadorIdioma>();
        services.AddSingleton<IDetectorIntencao, DetectorIntencao>();
        services.AddSingleton<ConvocadorMemoria>();
        services.AddSingleton<ResolvedorConversa>();
        services.AddSingleton<IGerenciadorIdioma, GerenciadorIdioma>();
        services.AddSingleton<ILocalizadorResposta, LocalizadorResposta>();

        services.AddSingleton<IFerramenta, FerramentaInfo>();
        services.AddSingleton<IFerramenta, FerramentaArquivos>();
        services.AddSingleton<IFerramenta, FerramentaSistema>();
        services.AddSingleton<IFerramenta, FerramentaInternet>();
        services.AddSingleton<IFerramenta, FerramentaMemoria>();
        services.AddSingleton<IFerramenta, FerramentaProcessos>();
        services.AddSingleton<IFerramenta, FerramentaTerminal>();
        services.AddSingleton<IGit, Git>();
        services.AddSingleton<IFerramenta, FerramentaGit>();
        services.AddSingleton<IProjetoExecutor, ProjetoExecutor>();
        services.AddSingleton<IFerramenta, FerramentaProjeto>();
        services.AddSingleton<IDiagnostico, Diagnostico>();
        services.AddSingleton<IInterpretadorDiagnostico, InterpretadorDiagnostico>();
        services.AddSingleton<IFerramenta, FerramentaDiagnostico>();
        services.AddSingleton<IAnalisadorCodigo, AnalisadorCodigo>();
        services.AddSingleton<IFerramenta, FerramentaAnaliseCodigo>();
        services.AddSingleton<ICorretorCodigo, CorretorCodigo>();
        services.AddSingleton<IFerramenta, FerramentaCorrecaoCodigo>();
        services.AddSingleton<IGeradorAlteracaoCodigo, GeradorAlteracaoCodigo>();
        services.AddSingleton<ValidadorCorrecao>();
        services.AddSingleton<CicloCorrecao>();
        services.AddSingleton<ICicloCorrecao, CicloCorrecao>();
        services.AddSingleton<IAutocorretor, Autocorretor>();
        services.AddSingleton<IFerramenta, FerramentaAutocorrecao>();

        return services;
    }

    public static void
        RegistrarFerramentas(
            this IServiceProvider provider)
    {
        var catalogo =
            provider.GetRequiredService<CatalogoFerramentas>();

        var ferramentas =
            provider.GetServices<IFerramenta>();

        foreach (var ferramenta in ferramentas)
        {
            catalogo.Registrar(ferramenta);
        }
    }
}
