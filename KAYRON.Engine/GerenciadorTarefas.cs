using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class GerenciadorTarefas : IGerenciadorTarefas
{
    private readonly IPlanejador _planejador;
    private readonly IExecutorPlano _executorPlano;
    private readonly IContextoProjeto _contextoProjeto;

    public GerenciadorTarefas(
        IPlanejador planejador,
        IExecutorPlano executorPlano,
        IContextoProjeto contextoProjeto)
    {
        ArgumentNullException.ThrowIfNull(planejador);
        ArgumentNullException.ThrowIfNull(executorPlano);
        ArgumentNullException.ThrowIfNull(contextoProjeto);

        _planejador = planejador;
        _executorPlano = executorPlano;
        _contextoProjeto = contextoProjeto;
    }

    public async Task<Resposta> ExecutarAsync(
        string objetivo,
        IContexto contexto,
        CancellationToken cancellationToken = default,
        IntencaoDetectada? intencao = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(objetivo))
        {
            return new Resposta
            {
                Conteudo = "O objetivo da tarefa não foi informado."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var diretorioBase =
            ObterDiretorioBase(contexto);

        var projeto =
            _contextoProjeto.Detectar(
                diretorioBase);

        if (projeto is not null)
        {
            _contextoProjeto.Definir(projeto);

            contexto.Adicionar(
                "diretorio_projeto",
                projeto.Diretorio);

            if (!string.IsNullOrWhiteSpace(projeto.ArquivoProjeto))
            {
                contexto.Adicionar(
                    "arquivo_projeto",
                    projeto.ArquivoProjeto);
            }

            if (!string.IsNullOrWhiteSpace(projeto.Solucao))
            {
                contexto.Adicionar(
                    "solucao",
                    projeto.Solucao);
            }

            if (!string.IsNullOrWhiteSpace(projeto.Tipo))
            {
                contexto.Adicionar(
                    "tipo_projeto",
                    projeto.Tipo);
            }
        }

        var plano =
            _planejador.CriarPlano(
                objetivo,
                contexto);

        var resultado =
            await _executorPlano.ExecutarAsync(
                plano,
                contexto,
                cancellationToken);

        return ConverterResultado(resultado);
    }

    private static string ObterDiretorioBase(
        IContexto contexto)
    {
        var projeto =
            contexto.Obter(
                "diretorio_projeto");

        if (!string.IsNullOrWhiteSpace(projeto))
        {
            return projeto;
        }

        var diretorio =
            contexto.Obter(
                "diretorio");

        if (!string.IsNullOrWhiteSpace(diretorio))
        {
            return diretorio;
        }

        return Directory.GetCurrentDirectory();
    }

    private static Resposta ConverterResultado(
        ResultadoPlano resultado)
    {
        if (!resultado.Sucesso)
        {
            return new Resposta
            {
                Conteudo =
                    $"Erro: {resultado.Erro ?? "A execução do plano falhou."}"
            };
        }

        var respostas =
            resultado.Resultados;

        if (respostas.Count == 0)
        {
            return new Resposta
            {
                Conteudo =
                    "Plano executado sem resultados."
            };
        }

        var conteudo =
            string.Join(
                Environment.NewLine,
                respostas
                    .Select(r => r.Conteudo)
                    .Where(c => !string.IsNullOrWhiteSpace(c)));

        return new Resposta
        {
            Conteudo =
                string.IsNullOrWhiteSpace(conteudo)
                    ? "Plano executado com sucesso."
                    : conteudo
        };
    }
}
