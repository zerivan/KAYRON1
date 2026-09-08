using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaProjeto
    : IFerramenta
{
    private readonly IProjetoExecutor _executor;
    private readonly IContextoProjeto _contextoProjeto;

    public FerramentaProjeto(
        IProjetoExecutor executor,
        IContextoProjeto contextoProjeto)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(contextoProjeto);

        _executor = executor;
        _contextoProjeto = contextoProjeto;
    }

    public string Nome =>
        "projeto";

    public string Descricao =>
        "Analisa e executa operações de desenvolvimento no projeto atual.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "build",
                "test",
                "restore",
                "check"
            ],
            Exemplos =
            [
                "faça o build",
                "execute os testes",
                "restaure o projeto",
                "verifique o projeto"
            ]
        };

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var operacao =
            ObterOperacao(argumentos);

        if (string.IsNullOrWhiteSpace(operacao))
        {
            return ResultadoFerramenta.Falha(
                "Informe uma operação: build, test, restore ou check.");
        }

        var diretorio =
            contexto.Obter(
                "diretorio_projeto")
            ?? Environment.CurrentDirectory;

        var projeto =
            _contextoProjeto.Detectar(
                diretorio);

        if (projeto is null)
        {
            return ResultadoFerramenta.Falha(
                $"Nenhum projeto reconhecido em: {diretorio}");
        }

        contexto.Adicionar(
            "projeto_raiz",
            projeto.Diretorio);

        contexto.Adicionar(
            "projeto_tipo",
            projeto.Tipo ?? string.Empty);

        contexto.Adicionar(
            "projeto_arquivo",
            projeto.ArquivoProjeto ?? string.Empty);

        var resultado =
            await _executor.ExecutarAsync(
                operacao,
                projeto.Diretorio,
                cancellationToken);

        contexto.Adicionar(
            "projeto_operacao",
            operacao);

        contexto.Adicionar(
            "projeto_status",
            resultado.Sucesso
                ? "sucesso"
                : "falha");

        contexto.Adicionar(
            "projeto_saida",
            resultado.Sucesso
                ? resultado.Saida
                : resultado.Erro);

        if (!resultado.Sucesso)
        {
            return ResultadoFerramenta.Falha(
                $"Projeto falhou em '{operacao}' " +
                $"(código {resultado.CodigoSaida}): " +
                resultado.Erro);
        }

        return ResultadoFerramenta.Ok(
            string.IsNullOrWhiteSpace(resultado.Saida)
                ? $"Operação '{operacao}' concluída com sucesso."
                : resultado.Saida);
    }

    private static string ObterOperacao(
        string argumentos)
    {
        var texto =
            argumentos.Trim();

        foreach (var operacao in
                 new[]
                 {
                     "build",
                     "test",
                     "restore",
                     "check"
                 })
        {
            if (texto.Contains(
                    operacao,
                    StringComparison.OrdinalIgnoreCase))
            {
                return operacao;
            }
        }

        return string.Empty;
    }
}
