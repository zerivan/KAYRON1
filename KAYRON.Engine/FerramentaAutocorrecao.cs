using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaAutocorrecao
    : IFerramenta
{
    private readonly IAutocorretor _autocorretor;
    private readonly IContextoProjeto _contextoProjeto;

    public FerramentaAutocorrecao(
        IAutocorretor autocorretor,
        IContextoProjeto contextoProjeto)
    {
        ArgumentNullException.ThrowIfNull(autocorretor);
        ArgumentNullException.ThrowIfNull(contextoProjeto);

        _autocorretor = autocorretor;
        _contextoProjeto = contextoProjeto;
    }

    public string Nome =>
        "autocorrecao";

    public string Descricao =>
        "Executa o ciclo de build, diagnóstico, análise, correção e validação de um projeto.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "autocorrigir",
                "autocorreção",
                "corrigir projeto",
                "corrigir erros"
            ],
            Exemplos =
            [
                "encontre e analise o erro"
            ]
        };

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        cancellationToken.ThrowIfCancellationRequested();

        var diretorio =
            contexto.Obter(
                "diretorio_projeto")
            ?? contexto.Obter(
                "diretorio")
            ?? Environment.CurrentDirectory;

        var projeto =
            _contextoProjeto.Detectar(
                diretorio);

        if (projeto is null)
        {
            return ResultadoFerramenta.Falha(
                $"Nenhum projeto foi localizado a partir de: {diretorio}");
        }

        _contextoProjeto.Definir(
            projeto);

        contexto.Adicionar(
            "diretorio_projeto",
            projeto.Diretorio);

        if (!string.IsNullOrWhiteSpace(
                projeto.ArquivoProjeto))
        {
            contexto.Adicionar(
                "arquivo_projeto",
                projeto.ArquivoProjeto);
        }

        if (!string.IsNullOrWhiteSpace(
                projeto.Solucao))
        {
            contexto.Adicionar(
                "solucao",
                projeto.Solucao);
        }

        if (!string.IsNullOrWhiteSpace(
                projeto.Tipo))
        {
            contexto.Adicionar(
                "tipo_projeto",
                projeto.Tipo);
        }

        var resultado =
            await _autocorretor.ExecutarAsync(
                projeto.Diretorio,
                cancellationToken);

        contexto.Adicionar(
            "autocorrecao_etapa",
            resultado.Etapa);

        contexto.Adicionar(
            "autocorrecao_status",
            resultado.Sucesso
                ? "sucesso"
                : "analise_necessaria");

        if (!string.IsNullOrWhiteSpace(
                resultado.Arquivo))
        {
            contexto.Adicionar(
                "autocorrecao_arquivo",
                resultado.Arquivo);
        }

        return resultado.Sucesso
            ? ResultadoFerramenta.Ok(
                resultado.Mensagem)
            : ResultadoFerramenta.Falha(
                resultado.Mensagem);
    }
}
