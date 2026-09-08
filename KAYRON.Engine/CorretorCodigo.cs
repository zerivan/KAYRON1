using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class CorretorCodigo
    : ICorretorCodigo
{
    public async Task<ResultadoCorrecao> AplicarAsync(
        AlteracaoCodigo alteracao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alteracao);

        if (string.IsNullOrWhiteSpace(alteracao.Arquivo))
        {
            return ResultadoCorrecao.Falha(
                string.Empty,
                "O arquivo da alteração não foi informado.");
        }

        if (!File.Exists(alteracao.Arquivo))
        {
            return ResultadoCorrecao.Falha(
                alteracao.Arquivo,
                "O arquivo não existe.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string conteudoAtual;

        try
        {
            conteudoAtual =
                await File.ReadAllTextAsync(
                    alteracao.Arquivo,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            return ResultadoCorrecao.Falha(
                alteracao.Arquivo,
                $"Não foi possível ler o arquivo: {ex.Message}");
        }

        if (!string.Equals(
                conteudoAtual,
                alteracao.ConteudoOriginal,
                StringComparison.Ordinal))
        {
            return ResultadoCorrecao.Falha(
                alteracao.Arquivo,
                "O conteúdo atual do arquivo é diferente do conteúdo " +
                "original esperado. A alteração foi bloqueada para evitar " +
                "sobrescrever mudanças externas.");
        }

        if (string.Equals(
                alteracao.ConteudoOriginal,
                alteracao.ConteudoNovo,
                StringComparison.Ordinal))
        {
            return ResultadoCorrecao.Falha(
                alteracao.Arquivo,
                "A alteração não modifica o conteúdo do arquivo.");
        }

        var diretorioBackup =
            Path.Combine(
                Path.GetDirectoryName(alteracao.Arquivo)
                ?? Environment.CurrentDirectory,
                ".kayron-backups");

        Directory.CreateDirectory(
            diretorioBackup);

        var nomeArquivo =
            Path.GetFileName(
                alteracao.Arquivo);

        var backup =
            Path.Combine(
                diretorioBackup,
                $"{nomeArquivo}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak");

        try
        {
            await File.WriteAllTextAsync(
                backup,
                conteudoAtual,
                cancellationToken);

            await File.WriteAllTextAsync(
                alteracao.Arquivo,
                alteracao.ConteudoNovo,
                cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(backup))
                {
                    await File.WriteAllTextAsync(
                        alteracao.Arquivo,
                        conteudoAtual,
                        cancellationToken);
                }
            }
            catch
            {
            }

            return ResultadoCorrecao.Falha(
                alteracao.Arquivo,
                $"Falha ao aplicar a alteração: {ex.Message}");
        }

        return ResultadoCorrecao.Ok(
            alteracao.Arquivo,
            backup,
            string.IsNullOrWhiteSpace(alteracao.Motivo)
                ? "Alteração aplicada com backup."
                : alteracao.Motivo);
    }
}
