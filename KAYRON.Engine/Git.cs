using System.Diagnostics;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class Git : IGit
{
    public async Task<ResultadoGit> ExecutarAsync(
        string argumentos,
        string diretorio,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            return ResultadoGit.Falha(
                "O diretório do repositório não foi informado.");
        }

        if (!Directory.Exists(diretorio))
        {
            return ResultadoGit.Falha(
                $"Diretório não encontrado: {diretorio}");
        }

        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return ResultadoGit.Falha(
                "Os argumentos do Git não foram informados.");
        }

        var inicio =
            new ProcessStartInfo
            {
                FileName = "git.exe",
                WorkingDirectory = diretorio,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

        foreach (var argumento in SepararArgumentos(argumentos))
        {
            inicio.ArgumentList.Add(argumento);
        }

        using var processo =
            new Process
            {
                StartInfo = inicio,
                EnableRaisingEvents = true
            };

        try
        {
            if (!processo.Start())
            {
                return ResultadoGit.Falha(
                    "Não foi possível iniciar o Git.");
            }

            var leituraSaida =
                processo.StandardOutput.ReadToEndAsync(
                    cancellationToken);

            var leituraErro =
                processo.StandardError.ReadToEndAsync(
                    cancellationToken);

            await processo.WaitForExitAsync(
                cancellationToken);

            var saida =
                await leituraSaida;

            var erro =
                await leituraErro;

            if (processo.ExitCode == 0)
            {
                return ResultadoGit.Ok(
                    saida.Trim(),
                    processo.ExitCode);
            }

            return ResultadoGit.Falha(
                string.IsNullOrWhiteSpace(erro)
                    ? saida.Trim()
                    : erro.Trim(),
                processo.ExitCode);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!processo.HasExited)
                {
                    processo.Kill(true);
                }
            }
            catch
            {
            }

            throw;
        }
        catch (Exception ex)
        {
            return ResultadoGit.Falha(
                $"Erro ao executar Git: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> SepararArgumentos(
        string argumentos)
    {
        var resultado =
            new List<string>();

        var atual =
            new System.Text.StringBuilder();

        var dentroAspas = false;

        for (var i = 0; i < argumentos.Length; i++)
        {
            var caractere =
                argumentos[i];

            if (caractere == '"')
            {
                dentroAspas = !dentroAspas;
                continue;
            }

            if (char.IsWhiteSpace(caractere) &&
                !dentroAspas)
            {
                if (atual.Length > 0)
                {
                    resultado.Add(
                        atual.ToString());

                    atual.Clear();
                }

                continue;
            }

            atual.Append(caractere);
        }

        if (atual.Length > 0)
        {
            resultado.Add(
                atual.ToString());
        }

        return resultado;
    }
}

