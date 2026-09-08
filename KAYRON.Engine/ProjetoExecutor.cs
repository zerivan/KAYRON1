using System.Diagnostics;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class ProjetoExecutor
    : IProjetoExecutor
{
    private readonly IDetectorProjeto _detector;

    public ProjetoExecutor(
        IDetectorProjeto detector)
    {
        ArgumentNullException.ThrowIfNull(detector);

        _detector = detector;
    }

    public async Task<ResultadoProjeto> ExecutarAsync(
        string operacao,
        string diretorio,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            return ResultadoProjeto.Falha(
                operacao,
                string.Empty,
                "O diretório do projeto não foi informado.");
        }

        var raiz =
            _detector.DetectarRaiz(
                diretorio);

        if (string.IsNullOrWhiteSpace(raiz))
        {
            return ResultadoProjeto.Falha(
                operacao,
                string.Empty,
                $"Nenhum projeto reconhecido em: {diretorio}");
        }

        var arquivo =
            _detector.ObterArquivoProjeto(
                raiz);

        var tipo =
            DetectarTipo(
                arquivo);

        if (string.IsNullOrWhiteSpace(tipo))
        {
            return ResultadoProjeto.Falha(
                operacao,
                string.Empty,
                "Não foi possível identificar o tipo do projeto.");
        }

        var comando =
            ObterComando(
                tipo,
                operacao);

        if (comando is null)
        {
            return ResultadoProjeto.Falha(
                operacao,
                tipo,
                $"A operação '{operacao}' não é suportada para {tipo}.");
        }

        return await ExecutarComandoAsync(
            comando.Value.arquivo,
            comando.Value.argumentos,
            raiz,
            operacao,
            tipo,
            cancellationToken);
    }

    private static (
        string arquivo,
        string argumentos)? ObterComando(
        string tipo,
        string operacao)
    {
        return tipo switch
        {
            ".NET" => operacao.ToLowerInvariant() switch
            {
                "build" =>
                    ("dotnet", "build"),
                "test" =>
                    ("dotnet", "test"),
                "restore" =>
                    ("dotnet", "restore"),
                "check" =>
                    ("dotnet", "build --no-restore"),
                _ => null
            },

            "Node.js" => operacao.ToLowerInvariant() switch
            {
                "build" =>
                    ("npm", "run build"),
                "test" =>
                    ("npm", "test"),
                "restore" =>
                    ("npm", "install"),
                "check" =>
                    ("npm", "run lint"),
                _ => null
            },

            "Python" => operacao.ToLowerInvariant() switch
            {
                "build" =>
                    ("python", "-m compileall ."),
                "test" =>
                    ("python", "-m pytest"),
                "restore" =>
                    ("python", "-m pip install -r requirements.txt"),
                "check" =>
                    ("python", "-m compileall ."),
                _ => null
            },

            "Rust" => operacao.ToLowerInvariant() switch
            {
                "build" =>
                    ("cargo", "build"),
                "test" =>
                    ("cargo", "test"),
                "restore" =>
                    ("cargo", "fetch"),
                "check" =>
                    ("cargo", "check"),
                _ => null
            },

            "Go" => operacao.ToLowerInvariant() switch
            {
                "build" =>
                    ("go", "build ./..."),
                "test" =>
                    ("go", "test ./..."),
                "restore" =>
                    ("go", "mod download"),
                "check" =>
                    ("go", "test ./..."),
                _ => null
            },

            "Java" => operacao.ToLowerInvariant() switch
            {
                "build" =>
                    ("mvn", "package"),
                "test" =>
                    ("mvn", "test"),
                "restore" =>
                    ("mvn", "dependency:resolve"),
                "check" =>
                    ("mvn", "verify"),
                _ => null
            },

            _ => null
        };
    }

    private static async Task<ResultadoProjeto> ExecutarComandoAsync(
        string arquivo,
        string argumentos,
        string diretorio,
        string operacao,
        string tipo,
        CancellationToken cancellationToken)
    {
        var inicio =
            new ProcessStartInfo
            {
                FileName = arquivo,
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
                StartInfo = inicio
            };

        try
        {
            if (!processo.Start())
            {
                return ResultadoProjeto.Falha(
                    operacao,
                    tipo,
                    $"Não foi possível iniciar: {arquivo}");
            }

            var saidaTask =
                processo.StandardOutput.ReadToEndAsync(
                    cancellationToken);

            var erroTask =
                processo.StandardError.ReadToEndAsync(
                    cancellationToken);

            await processo.WaitForExitAsync(
                cancellationToken);

            var saida =
                await saidaTask;

            var erro =
                await erroTask;

            if (processo.ExitCode == 0)
            {
                return ResultadoProjeto.Ok(
                    operacao,
                    tipo,
                    saida.Trim(),
                    processo.ExitCode);
            }

            return ResultadoProjeto.Falha(
                operacao,
                tipo,
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
            return ResultadoProjeto.Falha(
                operacao,
                tipo,
                $"Erro ao executar {arquivo}: {ex.Message}");
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

        foreach (var caractere in argumentos)
        {
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

    private static string DetectarTipo(
        string? arquivo)
    {
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            return string.Empty;
        }

        var nome =
            Path.GetFileName(
                arquivo);

        if (nome.Equals(
                "package.json",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Node.js";
        }

        if (nome.Equals(
                "Cargo.toml",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Rust";
        }

        if (nome.Equals(
                "go.mod",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Go";
        }

        if (nome.Equals(
                "pyproject.toml",
                StringComparison.OrdinalIgnoreCase) ||
            nome.Equals(
                "requirements.txt",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Python";
        }

        if (nome.Equals(
                "pom.xml",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Java";
        }

        if (nome.EndsWith(
                ".csproj",
                StringComparison.OrdinalIgnoreCase) ||
            nome.EndsWith(
                ".sln",
                StringComparison.OrdinalIgnoreCase) ||
            nome.EndsWith(
                ".slnx",
                StringComparison.OrdinalIgnoreCase))
        {
            return ".NET";
        }

        return string.Empty;
    }
}
