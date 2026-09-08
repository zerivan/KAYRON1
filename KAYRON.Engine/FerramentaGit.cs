using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaGit : IFerramenta
{
    private readonly IGit _git;

    public FerramentaGit(IGit git)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
    }

    public string Nome =>
        "git";

    public string Descricao =>
        "Gerencia repositórios Git locais.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "status",
                "log",
                "branch",
                "diff",
                "add",
                "commit",
                "push",
                "pull",
                "checkout",
                "merge"
            ],
            Exemplos =
            [
                "git status",
                "mostre o git status",
                "git log",
                "mostre as branches",
                "git diff",
                "git add .",
                "git commit -m \"correção\"",
                "git push",
                "git pull"
            ]
        };

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var comando =
            ExtrairComando(argumentos);

        if (string.IsNullOrWhiteSpace(comando))
        {
            return ResultadoFerramenta.Falha(
                "Informe uma operação Git.");
        }

        var diretorio =
            ObterDiretorio(contexto);

        contexto.Adicionar(
            "git_diretorio",
            diretorio);

        contexto.Adicionar(
            "git_comando",
            comando);

        var resultado =
            await _git.ExecutarAsync(
                comando,
                diretorio,
                cancellationToken);

        if (!resultado.Sucesso)
        {
            contexto.Adicionar(
                "git_status",
                "falha");

            return ResultadoFerramenta.Falha(
                $"Git falhou (código {resultado.CodigoSaida}): " +
                resultado.Erro);
        }

        contexto.Adicionar(
            "git_status",
            "sucesso");

        contexto.Adicionar(
            "git_saida",
            resultado.Saida);

        return ResultadoFerramenta.Ok(
            string.IsNullOrWhiteSpace(resultado.Saida)
                ? "Operação Git concluída."
                : resultado.Saida);
    }

    private static string ExtrairComando(
        string argumentos)
    {
        var texto =
            argumentos.Trim();

        if (texto.StartsWith(
                "git ",
                StringComparison.OrdinalIgnoreCase))
        {
            return texto[4..].Trim();
        }

        return texto;
    }

    private static string ObterDiretorio(
        IContexto contexto)
    {
        var projeto =
            contexto.Obter("diretorio_projeto");

        if (!string.IsNullOrWhiteSpace(projeto) &&
            Directory.Exists(projeto))
        {
            return projeto;
        }

        return Environment.CurrentDirectory;
    }
}
