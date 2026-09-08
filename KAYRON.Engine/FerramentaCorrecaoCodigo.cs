using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaCorrecaoCodigo
    : IFerramenta
{
    private readonly ICorretorCodigo _corretor;

    public FerramentaCorrecaoCodigo(
        ICorretorCodigo corretor)
    {
        ArgumentNullException.ThrowIfNull(corretor);

        _corretor = corretor;
    }

    public string Nome =>
        "correcao";

    public string Descricao =>
        "Aplica uma alteração de código localizada após validação do conteúdo original.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "corrigir",
                "correcao",
                "alterar código",
                "aplicar alteração"
            ],
            Exemplos =
            [
                "corrija o código",
                "aplique a correção",
                "corrija este erro"
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
                "correcao_arquivo");

        var original =
            contexto.Obter(
                "correcao_original");

        var novo =
            contexto.Obter(
                "correcao_novo");

        var motivo =
            contexto.Obter(
                "correcao_motivo")
            ?? argumentos;

        if (string.IsNullOrWhiteSpace(arquivo) ||
            original is null ||
            novo is null)
        {
            return ResultadoFerramenta.Falha(
                "A correção não possui arquivo, conteúdo original " +
                "e conteúdo novo completos.");
        }

        var alteracao =
            new AlteracaoCodigo
            {
                Arquivo = arquivo,
                ConteudoOriginal = original,
                ConteudoNovo = novo,
                Motivo = motivo
            };

        var resultado =
            await _corretor.AplicarAsync(
                alteracao,
                cancellationToken);

        if (!resultado.Sucesso)
        {
            return ResultadoFerramenta.Falha(
                resultado.Erro
                ?? resultado.Mensagem);
        }

        contexto.Adicionar(
            "correcao_aplicada",
            "sim");

        contexto.Adicionar(
            "correcao_backup",
            resultado.Backup);

        contexto.Adicionar(
            "correcao_arquivo",
            resultado.Arquivo);

        return ResultadoFerramenta.Ok(
            $"Correção aplicada com sucesso.{Environment.NewLine}" +
            $"Arquivo: {resultado.Arquivo}{Environment.NewLine}" +
            $"Backup: {resultado.Backup}{Environment.NewLine}" +
            $"Motivo: {resultado.Mensagem}");
    }
}
