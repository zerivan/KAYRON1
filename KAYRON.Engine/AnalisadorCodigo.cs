using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class AnalisadorCodigo
    : IAnalisadorCodigo
{
    public async Task<AnaliseCodigo> AnalisarAsync(
        DiagnosticoErro erro,
        string diretorioProjeto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(erro);

        if (string.IsNullOrWhiteSpace(erro.Arquivo))
        {
            return new AnaliseCodigo
            {
                Encontrado = false,
                Diagnostico =
                    "O diagnóstico não informa o arquivo relacionado ao problema."
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var caminho =
            ResolverCaminho(
                erro.Arquivo,
                diretorioProjeto);

        if (caminho is null)
        {
            return new AnaliseCodigo
            {
                Encontrado = false,
                Arquivo = erro.Arquivo,
                Linha = erro.Linha,
                Diagnostico =
                    $"O arquivo '{erro.Arquivo}' não foi localizado."
            };
        }

        var linhas =
            await File.ReadAllLinesAsync(
                caminho,
                cancellationToken);

        if (linhas.Length == 0)
        {
            return new AnaliseCodigo
            {
                Encontrado = true,
                Arquivo = caminho,
                Linha = erro.Linha,
                Diagnostico =
                    "O arquivo foi localizado, mas está vazio."
            };
        }

        var linhaAlvo =
            erro.Linha.GetValueOrDefault(1);

        linhaAlvo =
            Math.Clamp(
                linhaAlvo,
                1,
                linhas.Length);

        const int raio = 4;

        var inicio =
            Math.Max(
                1,
                linhaAlvo - raio);

        var fim =
            Math.Min(
                linhas.Length,
                linhaAlvo + raio);

        var trecho =
            string.Join(
                Environment.NewLine,
                Enumerable.Range(
                    inicio,
                    fim - inicio + 1)
                .Select(
                    numero =>
                        $"{numero,5}: {linhas[numero - 1]}"));

        var linhaCodigo =
            linhas[linhaAlvo - 1];

        var diagnostico =
            ConstruirDiagnostico(
                erro,
                linhaCodigo);

        var acao =
            ConstruirAcaoSugerida(
                erro,
                linhaCodigo);

        return new AnaliseCodigo
        {
            Encontrado = true,
            Arquivo = caminho,
            Linha = linhaAlvo,
            Trecho = trecho,
            Contexto = linhaCodigo,
            Diagnostico = diagnostico,
            AcaoSugerida = acao
        };
    }

    private static string? ResolverCaminho(
        string arquivo,
        string diretorioProjeto)
    {
        var candidatos =
            new List<string>();

        if (Path.IsPathRooted(arquivo))
        {
            candidatos.Add(arquivo);
        }
        else
        {
            candidatos.Add(
                Path.Combine(
                    diretorioProjeto,
                    arquivo));

            candidatos.Add(
                Path.Combine(
                    Environment.CurrentDirectory,
                    arquivo));
        }

        foreach (var candidato in candidatos)
        {
            try
            {
                if (File.Exists(candidato))
                {
                    return Path.GetFullPath(candidato);
                }
            }
            catch
            {
            }
        }

        var nome =
            Path.GetFileName(arquivo);

        if (string.IsNullOrWhiteSpace(nome) ||
            !Directory.Exists(diretorioProjeto))
        {
            return null;
        }

        try
        {
            return Directory
                .EnumerateFiles(
                    diretorioProjeto,
                    nome,
                    SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string ConstruirDiagnostico(
        DiagnosticoErro erro,
        string linha)
    {
        if (!string.IsNullOrWhiteSpace(
                erro.Codigo))
        {
            return
                $"Erro {erro.Codigo} identificado na linha " +
                $"{erro.Linha}: {erro.Mensagem}. " +
                $"A linha analisada é: {linha.Trim()}";
        }

        return
            $"Problema identificado na linha " +
            $"{erro.Linha}: {erro.Mensagem}. " +
            $"A linha analisada é: {linha.Trim()}";
    }

    private static string ConstruirAcaoSugerida(
        DiagnosticoErro erro,
        string linha)
    {
        if (erro.Codigo.Equals(
                "CS1002",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar a sintaxe da instrução e a ausência de ';'.";
        }

        if (erro.Codigo.Equals(
                "CS0103",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar se o identificador existe, " +
                "está no escopo correto ou precisa de using.";
        }

        if (erro.Codigo.Equals(
                "CS0246",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar namespace, using, referência do projeto " +
                "ou dependência necessária.";
        }

        if (erro.Codigo.Equals(
                "CS1061",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar se o tipo possui o membro utilizado " +
                "e se o tipo recebido é realmente o esperado.";
        }

        if (erro.Codigo.Equals(
                "CS0029",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar incompatibilidade entre os tipos " +
                "atribuído e esperado.";
        }

        if (erro.Codigo.Equals(
                "CS1503",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar os tipos e a ordem dos argumentos " +
                "passados ao método.";
        }

        if (linha.Contains(
                "null",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "Verificar fluxo de nulidade e possibilidade " +
                "de referência nula.";
        }

        return
            "Inspecionar o trecho indicado, suas dependências " +
            "e o contexto de execução antes de modificar o código.";
    }
}
