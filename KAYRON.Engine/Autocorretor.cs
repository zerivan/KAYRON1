using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class Autocorretor : IAutocorretor
{
    private readonly ICicloCorrecao _cicloCorrecao;

    public Autocorretor(
        ICicloCorrecao cicloCorrecao)
    {
        ArgumentNullException.ThrowIfNull(cicloCorrecao);

        _cicloCorrecao = cicloCorrecao;
    }

    public async Task<ResultadoAutocorrecao> ExecutarAsync(
        string diretorioProjeto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorioProjeto))
        {
            return new ResultadoAutocorrecao
            {
                Sucesso = false,
                Etapa = "entrada",
                Mensagem = "O diretório do projeto não foi informado."
            };
        }

        var diretorio =
            Path.GetFullPath(
                diretorioProjeto.Trim());

        if (!Directory.Exists(diretorio))
        {
            return new ResultadoAutocorrecao
            {
                Sucesso = false,
                Etapa = "entrada",
                Mensagem =
                    $"Diretório do projeto não encontrado: {diretorio}"
            };
        }

        try
        {
            var resultado =
                await _cicloCorrecao.ExecutarAsync(
                    diretorio,
                    cancellationToken);

            if (resultado.Sucesso)
            {
                return new ResultadoAutocorrecao
                {
                    Sucesso = true,
                    Etapa =
                        resultado.AlteracaoAplicada
                            ? "validado"
                            : "sem_alteracao",
                    Arquivo = resultado.Arquivo,
                    Mensagem = resultado.Mensagem
                };
            }

            return new ResultadoAutocorrecao
            {
                Sucesso = false,
                Etapa =
                    resultado.RollbackExecutado
                        ? "rollback"
                        : "falha",
                Arquivo = resultado.Arquivo,
                Mensagem = resultado.Mensagem
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ResultadoAutocorrecao
            {
                Sucesso = false,
                Etapa = "erro",
                Mensagem =
                    $"Erro no ciclo de autocorreção: {ex.Message}"
            };
        }
    }
}
