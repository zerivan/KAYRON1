using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class FerramentaArquivos : IFerramenta
{
    public string Nome => "arquivos";

    public string Descricao =>
        "Lista, verifica e lê arquivos do sistema.";

    public CapacidadeFerramenta Capacidade =>
        new()
        {
            Nome = Nome,
            Descricao = Descricao,
            Operacoes =
            [
                "listar",
                "existe",
                "ler"
            ],
            Exemplos =
            [
                "liste os arquivos de C:\\Meus Projetos",
                "verifique se existe C:\\teste.txt",
                "leia o arquivo C:\\teste.txt"
            ]
        };
    public Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return Task.FromResult(
                ResultadoFerramenta.Falha(
                    "Informe uma operação. Use: listar <caminho>, existe <caminho> ou ler <caminho>"));
        }

        var partes = argumentos.Trim().Split(
            ' ',
            2,
            StringSplitOptions.RemoveEmptyEntries);

        var operacao = partes[0]
            .Trim('"', '“', '”', '\'', '‘', '’', '`', '[', ']', '(', ')', ':', ',', ';', '.', ' ')
            .ToLowerInvariant();

        var caminho =
            partes.Length > 1
                ? partes[1].Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(caminho))
        {
            return Task.FromResult(
                ResultadoFerramenta.Falha(
                    "O caminho não foi informado."));
        }

        try
        {
            return operacao switch
            {
                "listar" =>
                    Task.FromResult(Listar(caminho)),

                "existe" =>
                    Task.FromResult(Existe(caminho)),

                "ler" =>
                    Task.FromResult(Ler(caminho)),

                _ =>
                    Task.FromResult(
                        ResultadoFerramenta.Falha(
                            $"Operação desconhecida: {operacao}"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                ResultadoFerramenta.Falha(
                    $"Erro ao acessar arquivos: {ex.Message}"));
        }
    }

    private static ResultadoFerramenta Listar(
        string caminho)
    {
        if (!Directory.Exists(caminho))
        {
            return ResultadoFerramenta.Falha(
                $"Diretório não encontrado: {caminho}");
        }

        var itens = Directory
            .EnumerateFileSystemEntries(caminho)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (itens.Length == 0)
        {
            return ResultadoFerramenta.Ok(
                "O diretório está vazio.");
        }

        return ResultadoFerramenta.Ok(
            string.Join(
                Environment.NewLine,
                itens));
    }

    private static ResultadoFerramenta Existe(
        string caminho)
    {
        var existe =
            File.Exists(caminho) ||
            Directory.Exists(caminho);

        return ResultadoFerramenta.Ok(
            existe
                ? "Existe."
                : "Não existe.");
    }

    private static ResultadoFerramenta Ler(
        string caminho)
    {
        if (!File.Exists(caminho))
        {
            return ResultadoFerramenta.Falha(
                $"Arquivo não encontrado: {caminho}");
        }

        var conteudo = File.ReadAllText(caminho);

        return ResultadoFerramenta.Ok(conteudo);
    }
}


