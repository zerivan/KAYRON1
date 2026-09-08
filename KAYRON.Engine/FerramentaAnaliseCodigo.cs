using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaAnaliseCodigo
    : IFerramenta
{
    private readonly IAnalisadorCodigo _analisador;

    public FerramentaAnaliseCodigo(
        IAnalisadorCodigo analisador)
    {
        ArgumentNullException.ThrowIfNull(analisador);

        _analisador = analisador;
    }

    public string Nome =>
        "analise";

    public string Descricao =>
        "Analisa um erro de desenvolvimento e o código correspondente.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "analisar",
                "análise",
                "diagnosticar",
                "causa",
                "código"
            ],
            Exemplos =
            [
                "analise este erro",
                "analise a causa do erro",
                "analise o código do erro"
            ]
        };

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var arquivo =
            contexto.Obter(
                "diagnostico_arquivo");

        var linhaTexto =
            contexto.Obter(
                "diagnostico_linha");

        var codigo =
            contexto.Obter(
                "diagnostico_codigo")
            ?? string.Empty;

        var mensagem =
            contexto.Obter(
                "diagnostico_mensagem")
            ?? argumentos;

        if (string.IsNullOrWhiteSpace(arquivo))
        {
            return ResultadoFerramenta.Falha(
                "Nenhum arquivo de diagnóstico está disponível para análise.");
        }

        int? linha = null;

        if (int.TryParse(
                linhaTexto,
                out var linhaConvertida))
        {
            linha = linhaConvertida;
        }

        var erro =
            new DiagnosticoErro
            {
                Arquivo = arquivo,
                Linha = linha,
                Codigo = codigo,
                Mensagem = mensagem,
                Severidade = "error",
                Origem = "diagnostico"
            };

        var diretorioProjeto =
            contexto.Obter(
                "diretorio_projeto")
            ?? Environment.CurrentDirectory;

        var resultado =
            await _analisador.AnalisarAsync(
                erro,
                diretorioProjeto,
                cancellationToken);

        contexto.Adicionar(
            "analise_codigo_arquivo",
            resultado.Arquivo);

        contexto.Adicionar(
            "analise_codigo_linha",
            resultado.Linha?.ToString() ?? string.Empty);

        contexto.Adicionar(
            "analise_codigo_diagnostico",
            resultado.Diagnostico);

        contexto.Adicionar(
            "analise_codigo_acao",
            resultado.AcaoSugerida);

        if (!resultado.Encontrado)
        {
            return ResultadoFerramenta.Falha(
                resultado.Diagnostico);
        }

        return ResultadoFerramenta.Ok(
            $"DIAGNÓSTICO:{Environment.NewLine}" +
            $"{resultado.Diagnostico}" +
            Environment.NewLine +
            Environment.NewLine +
            $"TRECHO:{Environment.NewLine}" +
            $"{resultado.Trecho}" +
            Environment.NewLine +
            Environment.NewLine +
            $"AÇÃO SUGERIDA:{Environment.NewLine}" +
            $"{resultado.AcaoSugerida}");
    }
}
