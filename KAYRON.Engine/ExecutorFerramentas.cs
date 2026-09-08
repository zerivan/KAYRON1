using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ExecutorFerramentas
{
    private readonly ICatalogoFerramentas _catalogo;
    private readonly IAutorizadorFerramentas _autorizador;
    private readonly IConfirmadorOperacao _confirmador;

    public ExecutorFerramentas(
        ICatalogoFerramentas catalogo,
        IAutorizadorFerramentas autorizador,
        IConfirmadorOperacao confirmador)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(autorizador);
        ArgumentNullException.ThrowIfNull(confirmador);

        _catalogo = catalogo;
        _autorizador = autorizador;
        _confirmador = confirmador;
    }

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string nome,
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default,
        string? operacaoSolicitada = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(nome))
        {
            return ResultadoFerramenta.Falha(
                "O nome da ferramenta não foi informado.");
        }

        var ferramenta =
            _catalogo.Obter(nome);

        if (ferramenta is null)
        {
            return ResultadoFerramenta.Falha(
                $"Ferramenta não encontrada: {nome}");
        }

        var autorizacao =
            _autorizador.Autorizar(
                ferramenta,
                argumentos,
                operacaoSolicitada);

        if (!autorizacao.Permitido)
        {
            if (autorizacao.ExigeConfirmacao)
            {
                var pendente =
                    _confirmador.ObterPendente(contexto);

                var operacao =
                    ObterOperacao(
                        ferramenta,
                        argumentos,
                        operacaoSolicitada);

                if (pendente is not null &&
                    pendente.Ferramenta.Equals(
                        ferramenta.Nome,
                        StringComparison.OrdinalIgnoreCase) &&
                    pendente.Operacao.Equals(
                        operacao,
                        StringComparison.OrdinalIgnoreCase) &&
                    pendente.Argumentos.Equals(
                        argumentos,
                        StringComparison.Ordinal))
                {
                    return ResultadoFerramenta.Falha(
                        $"CONFIRMAÇÃO NECESSÁRIA: {pendente.Motivo}" +
                        Environment.NewLine +
                        $"ID DA CONFIRMAÇÃO: {pendente.Id}" +
                        Environment.NewLine +
                        $"Ferramenta: {pendente.Ferramenta}" +
                        Environment.NewLine +
                        $"Operação: {pendente.Operacao}" +
                        Environment.NewLine +
                        $"Argumentos: {pendente.Argumentos}");
                }

                var solicitacao =
                    _confirmador.Criar(
                        ferramenta.Nome,
                        operacao,
                        argumentos,
                        autorizacao.Motivo,
                        contexto);

                return ResultadoFerramenta.Falha(
                    $"CONFIRMAÇÃO NECESSÁRIA: {solicitacao.Motivo}" +
                    Environment.NewLine +
                    $"ID DA CONFIRMAÇÃO: {solicitacao.Id}" +
                    Environment.NewLine +
                    $"Ferramenta: {solicitacao.Ferramenta}" +
                    Environment.NewLine +
                    $"Operação: {solicitacao.Operacao}" +
                    Environment.NewLine +
                    $"Argumentos: {solicitacao.Argumentos}" +
                    Environment.NewLine +
                    "Confirme explicitamente esta operação para continuar.");
            }

            return ResultadoFerramenta.Falha(
                $"OPERAÇÃO BLOQUEADA: {autorizacao.Motivo}");
        }

        try
        {
            return await ferramenta.ExecutarAsync(
                argumentos,
                contexto,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoFerramenta.Falha(
                $"Erro ao executar a ferramenta '{ferramenta.Nome}': {ex.Message}");
        }
    }

    private static string ObterOperacao(
        IFerramenta ferramenta,
        string argumentos,
        string? operacaoSolicitada)
    {
        if (!string.IsNullOrWhiteSpace(operacaoSolicitada))
        {
            foreach (var operacao in ferramenta.Capacidade.Operacoes)
            {
                if (operacao.Equals(
                        operacaoSolicitada,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return operacao;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(argumentos))
        {
            foreach (var operacao in ferramenta.Capacidade.Operacoes)
            {
                if (argumentos.Contains(
                        operacao,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return operacao;
                }
            }
        }

        return ferramenta.Capacidade.Operacoes.Count == 1
            ? ferramenta.Capacidade.Operacoes.First()
            : string.Empty;
    }
}
