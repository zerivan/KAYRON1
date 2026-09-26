using KAYRON.Core;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KAYRON.Engine;

public sealed class FerramentaInternet : IFerramenta
{
    private const string Endpoint = "https://www.bing.com/search";
    private readonly PesquisaWeb _pesquisaWeb;

    public FerramentaInternet(PesquisaWeb pesquisaWeb)
    {
        ArgumentNullException.ThrowIfNull(pesquisaWeb);
        _pesquisaWeb = pesquisaWeb;
    }

    public string Nome => "internet";

    public string Descricao =>
        "Pesquisa informa\u00e7\u00f5es atuais na Internet usando o \u00edndice web do Brave.";

    public CapacidadeFerramenta Capacidade => new()
    {
        Nome = Nome,
        Descricao = Descricao,
        Operacoes = new[] { "pesquisar" },
        Exemplos = new[]
        {
            "Pesquisar na web informa\u00e7\u00f5es atuais sobre Python",
            "Pesquisar fontes oficiais sobre .NET 10"
        }
    };

    public async Task<ResultadoFerramenta> ExecutarAsync(
        string argumentos,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        cancellationToken.ThrowIfCancellationRequested();

        var consulta = ExtrairConsulta(argumentos);
        if (string.IsNullOrWhiteSpace(consulta))
        {
            return ResultadoFerramenta.Falha(
                "Informe o que deve ser pesquisado na Internet.");
        }

        try
        {
            if (EhCotacaoDolar(consulta))
            {
                var cotacao = await _pesquisaWeb.ObterCotacaoDolarAsync(
                    cancellationToken);

                if (cotacao is null)
                {
                    return ResultadoFerramenta.Falha(
                        "N\u00e3o foi poss\u00edvel obter a cota\u00e7\u00e3o do d\u00f3lar em uma fonte oficial.");
                }

                var valor = cotacao.Value;
                return ResultadoFerramenta.Ok(
                    $"D\u00f3lar comercial \u2014 compra: R$ {valor.Compra:N4} | venda: R$ {valor.Venda:N4} | atualizado em {valor.DataHora:dd/MM/yyyy HH:mm:ss}.\nFonte oficial: Banco Central do Brasil.");
            }

            var resultados = await _pesquisaWeb.PesquisarAsync(
                consulta,
                10,
                cancellationToken);

            if (resultados.Count == 0)
            {
                return ResultadoFerramenta.Falha(
                    "A pesquisa web n\u00e3o retornou resultados utiliz\u00e1veis.");
            }

            var linhas = new List<string>
            {
                $"Pesquisa na Internet: {consulta}",
                "Fonte: pesquisa web",
                string.Empty
            };

            for (var indice = 0; indice < resultados.Count; indice++)
            {
                var resultado = resultados[indice];
                linhas.Add($"[{indice + 1}] {resultado.Titulo}");
                linhas.Add($"URL: {resultado.Url}");
                if (!string.IsNullOrWhiteSpace(resultado.Trecho))
                {
                    linhas.Add($"Resumo: {resultado.Trecho}");
                }
                linhas.Add(string.Empty);
            }

            return ResultadoFerramenta.Ok(
                string.Join(Environment.NewLine, linhas));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoFerramenta.Falha(
                $"Falha ao acessar a Internet: {ex.Message}");
        }
    }

    private static string ConstruirUrl(string consulta)
    {
        var parametros = new Dictionary<string, string>
        {
            ["q"] = consulta,
            ["count"] = "10",
            ["country"] = "BR",
            ["search_lang"] = "pt-br",
            ["safesearch"] = "moderate"
        };

        var query = string.Join(
            "&",
            parametros.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{Endpoint}?{query}";
    }

    private static string FormatarResultados(string json, string consulta)
    {
        using var documento = JsonDocument.Parse(json);
        if (!documento.RootElement.TryGetProperty("web", out var web) ||
            !web.TryGetProperty("results", out var resultados) ||
            resultados.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var linhas = new List<string>
        {
            $"Pesquisa na Internet: {consulta}",
            "Fonte: Brave Search",
            string.Empty
        };

        var indice = 0;
        foreach (var item in resultados.EnumerateArray())
        {
            if (indice >= 10)
            {
                break;
            }

            var titulo = ObterTexto(item, "title");
            var url = ObterTexto(item, "url");
            var descricao = ObterTexto(item, "description");

            if (string.IsNullOrWhiteSpace(titulo) ||
                string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            indice++;
            linhas.Add($"[{indice}] {titulo}");
            linhas.Add($"URL: {url}");
            if (!string.IsNullOrWhiteSpace(descricao))
            {
                linhas.Add($"Resumo: {descricao}");
            }
            linhas.Add(string.Empty);
        }

        return indice == 0 ? string.Empty : string.Join(Environment.NewLine, linhas);
    }

    private static string ObterTexto(JsonElement objeto, string nome)
    {
        return objeto.TryGetProperty(nome, out var valor) &&
               valor.ValueKind == JsonValueKind.String
            ? valor.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static string ExtrairErro(string conteudo)
    {
        try
        {
            using var documento = JsonDocument.Parse(conteudo);
            if (documento.RootElement.TryGetProperty("message", out var mensagem) &&
                mensagem.ValueKind == JsonValueKind.String)
            {
                return mensagem.GetString() ?? "erro desconhecido";
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(conteudo)
            ? "erro desconhecido"
            : conteudo.Trim();
    }

    private static bool EhCotacaoDolar(string texto)
    {
        var normalizado = texto.Trim().ToLowerInvariant()
            .Replace('\u00e1', 'a').Replace('\u00e0', 'a').Replace('\u00e3', 'a')
            .Replace('\u00e2', 'a').Replace('\u00e9', 'e').Replace('\u00ea', 'e')
            .Replace('\u00ed', 'i').Replace('\u00f3', 'o').Replace('\u00f4', 'o')
            .Replace('\u00f5', 'o').Replace('\u00fa', 'u').Replace('\u00e7', 'c');

        return normalizado.Contains("dolar") &&
               (normalizado.Contains("cotacao") ||
                normalizado.Contains("valor") ||
                normalizado.Contains("preco") ||
                normalizado.Contains("quanto") ||
                normalizado.Contains("compra") ||
                normalizado.Contains("venda"));
    }

    private static string ExtrairConsulta(string argumentos)
    {
        if (string.IsNullOrWhiteSpace(argumentos))
        {
            return string.Empty;
        }

        var texto = argumentos.Trim();
        try
        {
            using var documento = JsonDocument.Parse(texto);
            if (documento.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var nome in new[] { "consulta", "query", "instrucao" })
                {
                    if (documento.RootElement.TryGetProperty(nome, out var valor) &&
                        valor.ValueKind == JsonValueKind.String)
                    {
                        return valor.GetString()?.Trim() ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return texto;
    }
}
