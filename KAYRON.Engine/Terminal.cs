using System.Diagnostics;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class Terminal
    : ITerminal
{
    private static readonly string[] ComandosBloqueados =
    {
        "format ",
        "format.",
        "diskpart",
        "shutdown",
        "restart-computer",
        "stop-computer",
        "remove-item",
        "del /s",
        "del /q",
        "rd /s",
        "rmdir /s",
        "reg delete",
        "reg add",
        "bcdedit",
        "cipher /w",
        "takeown",
        "icacls ",
        "net user",
        "net localgroup",
        "sc delete",
        "sc stop",
        "taskkill /f",
        "powershell -encodedcommand",
        "powershell -enc ",
        "cmd /c format"
    };

    public async Task<ResultadoTerminal> ExecutarAsync(
        string comando,
        string? diretorio = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comando))
        {
            return ResultadoTerminal.Falha(
                "O comando do terminal não foi informado.");
        }

        if (ComandoBloqueado(comando))
        {
            return ResultadoTerminal.Falha(
                "O comando foi bloqueado pela política de segurança do KAYRON.");
        }

        var diretorioExecutar =
            string.IsNullOrWhiteSpace(diretorio)
                ? Environment.CurrentDirectory
                : diretorio.Trim();

        if (!Directory.Exists(diretorioExecutar))
        {
            return ResultadoTerminal.Falha(
                $"O diretório não existe: {diretorioExecutar}");
        }

        var inicio =
            new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /c \"{comando}\"",
                WorkingDirectory = diretorioExecutar,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

        using var processo =
            new Process
            {
                StartInfo = inicio
            };

        try
        {
            processo.Start();

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

            return ResultadoTerminal.Ok(
                processo.ExitCode,
                saida.Trim(),
                erro.Trim());
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!processo.HasExited)
                    processo.Kill(true);
            }
            catch
            {
            }

            throw;
        }
        catch (Exception ex)
        {
            return ResultadoTerminal.Falha(
                $"Falha ao executar o comando: {ex.Message}");
        }
    }

    private static bool ComandoBloqueado(
        string comando)
    {
        var normalizado =
            comando.Trim().ToLowerInvariant();

        return ComandosBloqueados.Any(
            bloqueado =>
                normalizado.StartsWith(
                    bloqueado,
                    StringComparison.OrdinalIgnoreCase));
    }
}
