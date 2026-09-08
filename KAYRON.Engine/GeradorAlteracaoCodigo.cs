using System.Text;
using System.Text.Json;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class GeradorAlteracaoCodigo : IGeradorAlteracaoCodigo
{
    private readonly IModeloInteligencia? _modeloInteligencia;

    public GeradorAlteracaoCodigo(
        IModeloInteligencia? modeloInteligencia = null)
    {
        _modeloInteligencia = modeloInteligencia;
    }

    public async Task<AlteracaoCodigo?> GerarAsync(
        AnaliseCodigo analise,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analise);

        if (!analise.Encontrado ||
            string.IsNullOrWhiteSpace(analise.Arquivo))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var modelo = _modeloInteligencia;

        if (modelo is not null)
        {
            var alteracao =
                await TentarGerarComInteligenciaAsync(
                    analise,
                    cancellationToken);

            if (alteracao is not null)
            {
                return alteracao;
            }
        }

        return GerarLocalmente(analise);
    }

    private async Task<AlteracaoCodigo?> TentarGerarComInteligenciaAsync(
        AnaliseCodigo analise,
        CancellationToken cancellationToken)
    {
        if (_modeloInteligencia is null)
        {
            return null;
        }

        var contexto = new Contexto();

        contexto.Adicionar(
            "modo",
            "correcao_codigo");

        contexto.Adicionar(
            "arquivo",
            analise.Arquivo);

        contexto.Adicionar(
            "linha",
            analise.Linha?.ToString() ?? string.Empty);

        contexto.Adicionar(
            "diagnostico",
            analise.Diagnostico ?? string.Empty);

        contexto.Adicionar(
            "trecho_codigo",
            analise.Trecho);

        var instrucao =
            CriarInstrucao(analise);

        var resposta =
            await _modeloInteligencia.GerarAsync(
                instrucao,
                contexto,
                cancellationToken);

        return InterpretarResposta(
            resposta,
            analise);
    }

    private static Instrucao CriarInstrucao(
        AnaliseCodigo analise)
    {
        var prompt =
            new StringBuilder()
                .AppendLine(
                    "Analise o erro de compilação abaixo e proponha uma correção mínima.")
                .AppendLine(
                    "Não altere arquitetura, contratos ou comportamento não relacionado.")
                .AppendLine(
                    "Retorne somente JSON válido.")
                .AppendLine(
                    "Formato:")
                .AppendLine(
                    "{")
                .AppendLine(
                    "  \"arquivo\": \"caminho\",")
                .AppendLine(
                    "  \"conteudoNovo\": \"conteúdo completo do arquivo\",")
                .AppendLine(
                    "  \"motivo\": \"explicação curta\"")
                .AppendLine(
                    "}")
                .AppendLine()
                .AppendLine(
                    $"Arquivo: {analise.Arquivo}")
                .AppendLine(
                    $"Linha: {analise.Linha}")
                .AppendLine(
                    $"Diagnóstico: {analise.Diagnostico ?? string.Empty}")
                .AppendLine()
                .AppendLine(
                    "Trecho:")
                .AppendLine(
                    analise.Trecho)
                .ToString();

        return new Instrucao
        {
            Conteudo = prompt
        };
    }

    private static AlteracaoCodigo? InterpretarResposta(
        Resposta resposta,
        AnaliseCodigo analise)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        if (string.IsNullOrWhiteSpace(
                resposta.Conteudo))
        {
            return null;
        }

        var texto =
            resposta.Conteudo.Trim();

        texto =
            RemoverBlocoMarkdown(texto);

        try
        {
            var documento =
                JsonSerializer.Deserialize<RespostaAlteracao>(
                    texto,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (documento is null)
            {
                return null;
            }

            var conteudoNovo =
                documento.ConteudoNovo ?? string.Empty;

            if (string.IsNullOrWhiteSpace(
                    conteudoNovo))
            {
                return null;
            }

            var arquivo =
                string.IsNullOrWhiteSpace(
                    documento.Arquivo)
                    ? (analise.Arquivo ?? string.Empty)
                    : (documento.Arquivo ?? string.Empty);

            if (string.IsNullOrWhiteSpace(
                    arquivo))
            {
                return null;
            }

            var caminho =
                Path.GetFullPath(arquivo);

            if (!File.Exists(caminho))
            {
                return null;
            }

            var original =
                File.ReadAllText(caminho);

            if (string.Equals(
                    original,
                    conteudoNovo,
                    StringComparison.Ordinal))
            {
                return null;
            }

            return new AlteracaoCodigo
            {
                Arquivo = caminho,
                ConteudoOriginal = original,
                ConteudoNovo = conteudoNovo,
                Motivo =
                    string.IsNullOrWhiteSpace(
                        documento.Motivo)
                        ? "Correção proposta pela inteligência."
                        : (documento.Motivo ?? string.Empty)
            };
        }
        catch
        {
            return null;
        }
    }

    private static string RemoverBlocoMarkdown(
        string texto)
    {
        if (!texto.StartsWith(
                "```",
                StringComparison.Ordinal))
        {
            return texto;
        }

        var primeiraQuebra =
            texto.IndexOf('\n');

        if (primeiraQuebra < 0)
        {
            return texto;
        }

        var ultimoBloco =
            texto.LastIndexOf(
                "```",
                StringComparison.Ordinal);

        if (ultimoBloco <= primeiraQuebra)
        {
            return texto;
        }

        return texto[
            (primeiraQuebra + 1)..ultimoBloco]
            .Trim();
    }

    private static AlteracaoCodigo? GerarLocalmente(
        AnaliseCodigo analise)
    {
        if (!EhDiagnosticoCS1002(
                analise.Diagnostico))
        {
            return null;
        }

        if (analise.Linha is null)
        {
            return null;
        }

        var caminho =
            Path.GetFullPath(
                analise.Arquivo);

        if (!File.Exists(caminho))
        {
            return null;
        }

        var original =
            File.ReadAllText(caminho);

        var separador =
            DetectarQuebraLinha(original);

        var linhas =
            original.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.None);

        var indice =
            analise.Linha.Value - 1;

        if (indice < 0 ||
            indice >= linhas.Length)
        {
            return null;
        }

        var linha =
            linhas[indice];

        var semEspacos =
            linha.TrimEnd();

        if (string.IsNullOrWhiteSpace(
                semEspacos))
        {
            return null;
        }

        if (semEspacos.EndsWith(";") ||
            semEspacos.EndsWith("{") ||
            semEspacos.EndsWith("}") ||
            semEspacos.EndsWith(":"))
        {
            return null;
        }

        linhas[indice] =
            semEspacos + ";";

        var novo =
            string.Join(
                separador,
                linhas);

        if (string.Equals(
                original,
                novo,
                StringComparison.Ordinal))
        {
            return null;
        }

        return new AlteracaoCodigo
        {
            Arquivo = caminho,
            ConteudoOriginal = original,
            ConteudoNovo = novo,
            Motivo =
                "Correção local determinística para CS1002."
        };
    }

    private static bool EhDiagnosticoCS1002(
        string diagnostico)
    {
        return diagnostico.Contains(
            "CS1002",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectarQuebraLinha(
        string texto)
    {
        return texto.Contains(
            "\r\n",
            StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
    }

    private sealed class RespostaAlteracao
    {
        public string Arquivo { get; set; } = string.Empty;
        public string ConteudoNovo { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
    }
}



