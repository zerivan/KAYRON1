using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class CicloCorrecao : ICicloCorrecao
{
    private const int MaxTentativas = 1;

    private readonly IProjetoExecutor _projetoExecutor;
    private readonly IDiagnostico _diagnostico;
    private readonly IAnalisadorCodigo _analisadorCodigo;
    private readonly IGeradorAlteracaoCodigo _geradorAlteracaoCodigo;
    private readonly ICorretorCodigo _corretorCodigo;

    public CicloCorrecao(
        IProjetoExecutor projetoExecutor,
        IDiagnostico diagnostico,
        IAnalisadorCodigo analisadorCodigo,
        IGeradorAlteracaoCodigo geradorAlteracaoCodigo,
        ICorretorCodigo corretorCodigo)
    {
        ArgumentNullException.ThrowIfNull(projetoExecutor);
        ArgumentNullException.ThrowIfNull(diagnostico);
        ArgumentNullException.ThrowIfNull(analisadorCodigo);
        ArgumentNullException.ThrowIfNull(geradorAlteracaoCodigo);
        ArgumentNullException.ThrowIfNull(corretorCodigo);

        _projetoExecutor = projetoExecutor;
        _diagnostico = diagnostico;
        _analisadorCodigo = analisadorCodigo;
        _geradorAlteracaoCodigo = geradorAlteracaoCodigo;
        _corretorCodigo = corretorCodigo;
    }

    public async Task<ResultadoCicloCorrecao> ExecutarAsync(
        string diretorioProjeto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorioProjeto))
        {
            return new ResultadoCicloCorrecao
            {
                Sucesso = false,
                Mensagem = "O diret?rio do projeto n?o foi informado."
            };
        }

        var diretorio = Path.GetFullPath(diretorioProjeto.Trim());

        if (!Directory.Exists(diretorio))
        {
            return new ResultadoCicloCorrecao
            {
                Sucesso = false,
                Mensagem = $"Diret?rio n?o encontrado: {diretorio}"
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var validacaoInicial =
            await ValidarAsync(
                diretorio,
                cancellationToken);

        if (validacaoInicial.Sucesso)
        {
            return new ResultadoCicloCorrecao
            {
                Sucesso = true,
                ValidacaoPassou = true,
                Tentativas = 0,
                Mensagem = "O projeto j? est? v?lido. Nenhuma corre??o foi necess?ria."
            };
        }

        var resultadoDiagnostico =
            _diagnostico.Analisar(
                validacaoInicial.Saida,
                string.Empty,
                "build");

        if (!resultadoDiagnostico.EncontrouErros ||
            resultadoDiagnostico.Problemas.Count == 0)
        {
            return new ResultadoCicloCorrecao
            {
                Sucesso = false,
                ValidacaoPassou = false,
                Tentativas = 0,
                Mensagem = "A valida??o falhou, mas nenhum erro estruturado foi encontrado."
            };
        }

        var erro =
            resultadoDiagnostico.Problemas.FirstOrDefault(
                problema =>
                    !string.Equals(
                        problema.Severidade,
                        "warning",
                        StringComparison.OrdinalIgnoreCase))
            ?? resultadoDiagnostico.Problemas.First();

        cancellationToken.ThrowIfCancellationRequested();

        var analise =
            await _analisadorCodigo.AnalisarAsync(
                erro,
                diretorio,
                cancellationToken);

        if (!analise.Encontrado)
        {
            return new ResultadoCicloCorrecao
            {
                Sucesso = false,
                ValidacaoPassou = false,
                Tentativas = 0,
                Arquivo = analise.Arquivo,
                Mensagem = "O erro foi identificado, mas o c?digo correspondente n?o foi localizado."
            };
        }

        var tentativas = 0;

        while (tentativas < MaxTentativas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            tentativas++;

            var alteracao =
                await _geradorAlteracaoCodigo.GerarAsync(
                    analise,
                    cancellationToken);

            if (alteracao is null)
            {
                return new ResultadoCicloCorrecao
                {
                    Sucesso = false,
                    ValidacaoPassou = false,
                    Tentativas = tentativas,
                    Arquivo = analise.Arquivo,
                    Mensagem = "N?o foi poss?vel gerar uma altera??o segura para o diagn?stico."
                };
            }

            var correcao =
                await _corretorCodigo.AplicarAsync(
                    alteracao,
                    cancellationToken);

            if (!correcao.Sucesso)
            {
                return new ResultadoCicloCorrecao
                {
                    Sucesso = false,
                    AlteracaoAplicada = false,
                    ValidacaoPassou = false,
                    Tentativas = tentativas,
                    Arquivo = correcao.Arquivo,
                    Backup = correcao.Backup,
                    Mensagem = correcao.Mensagem
                };
            }

            var validacaoFinal =
                await ValidarAsync(
                    diretorio,
                    cancellationToken);

            if (validacaoFinal.Sucesso)
            {
                return new ResultadoCicloCorrecao
                {
                    Sucesso = true,
                    AlteracaoAplicada = true,
                    ValidacaoPassou = true,
                    RollbackExecutado = false,
                    Tentativas = tentativas,
                    Arquivo = correcao.Arquivo,
                    Backup = correcao.Backup,
                    Mensagem = "Corre??o aplicada e validada com sucesso."
                };
            }

            var rollback =
                await ExecutarRollbackAsync(
                    correcao);

            return new ResultadoCicloCorrecao
            {
                Sucesso = false,
                AlteracaoAplicada = true,
                ValidacaoPassou = false,
                RollbackExecutado = rollback,
                Tentativas = tentativas,
                Arquivo = correcao.Arquivo,
                Backup = correcao.Backup,
                Mensagem = rollback
                    ? "A corre??o foi aplicada, mas a valida??o falhou. O arquivo foi restaurado."
                    : "A valida??o falhou e o rollback n?o p?de ser confirmado."
            };
        }

        return new ResultadoCicloCorrecao
        {
            Sucesso = false,
            Tentativas = tentativas,
            Mensagem = "O limite de tentativas de corre??o foi atingido."
        };
    }

    private async Task<(bool Sucesso, string Saida)> ValidarAsync(
        string diretorioProjeto,
        CancellationToken cancellationToken)
    {
        var resultado =
            await _projetoExecutor.ExecutarAsync(
                "build",
                diretorioProjeto,
                cancellationToken);

        return (
            resultado.Sucesso,
            string.Join(Environment.NewLine, new[] { resultado.Saida, resultado.Erro }.Where(valor => !string.IsNullOrWhiteSpace(valor))));
    }

    private static async Task<bool> ExecutarRollbackAsync(
        ResultadoCorrecao correcao)
    {
        if (string.IsNullOrWhiteSpace(correcao.Arquivo) ||
            string.IsNullOrWhiteSpace(correcao.Backup) ||
            !File.Exists(correcao.Arquivo) ||
            !File.Exists(correcao.Backup))
        {
            return false;
        }

        try
        {
            var conteudoBackup =
                await File.ReadAllTextAsync(
                    correcao.Backup,
                    CancellationToken.None);

            await File.WriteAllTextAsync(
                correcao.Arquivo,
                conteudoBackup,
                CancellationToken.None);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
