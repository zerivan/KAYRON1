using System.Security.Cryptography;
using System.Text;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class GestorConhecimento
{
    private readonly DeepSearch _deepSearch;
    private readonly IMemoriaAprendida _memoriaAprendida;

    public GestorConhecimento(
        ConvocadorMemoria convocadorMemoria,
        DeepSearch deepSearch,
        IMemoriaAprendida memoriaAprendida)
    {
        ArgumentNullException.ThrowIfNull(convocadorMemoria);
        ArgumentNullException.ThrowIfNull(deepSearch);
        ArgumentNullException.ThrowIfNull(memoriaAprendida);

        _ = convocadorMemoria;
        _deepSearch = deepSearch;
        _memoriaAprendida = memoriaAprendida;
    }

    public async Task<bool> AdquirirSeNecessarioAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        var entrada = instrucao.Conteudo.Trim();
        if (string.IsNullOrWhiteSpace(entrada))
            return false;

        var pesquisaSolicitada = string.Equals(
            contexto.Obter("intencao_detectada"),
            "internet:pesquisar",
            StringComparison.OrdinalIgnoreCase);

        if (!pesquisaSolicitada)
        {
            var memoriaExistente = _memoriaAprendida
                .Pesquisar(entrada, 3)
                .FirstOrDefault(m =>
                    string.Equals(m.Tipo, "conhecimento", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Origem, "pesquisa_verificada", StringComparison.OrdinalIgnoreCase) &&
                    m.Tags.Any(tag =>
                        string.Equals(tag, "verificado", StringComparison.OrdinalIgnoreCase)));

            if (memoriaExistente is not null)
            {
                contexto.Adicionar("memoria:aprendida_relevante", memoriaExistente.Chave);
                contexto.Adicionar($"memoria:aprendida:{memoriaExistente.Chave}", memoriaExistente.Valor);
                contexto.Adicionar("memoria:aprendida_verificada", "sim");
                contexto.Adicionar("conhecimento:estado", "recuperado");
                contexto.Adicionar("conhecimento:resposta", memoriaExistente.Valor);
                return false;
            }

            // Memória sem marca de verificação é apenas uma pista.
            // Não pode encerrar o turno: o KAYRON deve pesquisar antes de afirmar o fato.
            contexto.Adicionar("memoria:aprendida_verificada", "nao");
        }

        // Remove resultados transitórios do turno anterior antes de consultar a memória.
        contexto.Adicionar("conhecimento:resposta", string.Empty);
        contexto.Adicionar("memoria:aprendida_relevante", string.Empty);

        contexto.Adicionar(
            "conhecimento:estado",
            "nao_disponivel");

        contexto.Adicionar(
            "conhecimento:consulta",
            entrada);

        var consultaPesquisa = ConstruirConsultaPesquisa(entrada);

        contexto.Adicionar(
            "conhecimento:consulta_pesquisa",
            consultaPesquisa);

        var pesquisa = await _deepSearch.GerarAsync(
            new Instrucao
            {
                Conteudo = consultaPesquisa
            },
            CriarContextoPesquisa(contexto),
            cancellationToken);

        var conhecimentoValido = EhConhecimentoValido(pesquisa.Conteudo);

        if (!conhecimentoValido)
        {
            contexto.Adicionar(
                "conhecimento:estado",
                "nao_adquirido");

            return false;
        }

        var chave = CriarChave(entrada);

        if (!string.Equals(
                contexto.Obter("correcao_resposta:ativa"),
                "sim",
                StringComparison.OrdinalIgnoreCase))
        {
            _memoriaAprendida.Aprender(
                chave,
                pesquisa.Conteudo,
                "conhecimento",
                3,
                false,
                "pesquisa_verificada",
                new[] { "conhecimento", "pesquisa", "verificado" });
        }

        contexto.Adicionar(
            "conhecimento:estado",
            "adquirido");

        contexto.Adicionar(
            "conhecimento:chave",
            chave);

        contexto.Adicionar(
            "conhecimento:adquirido",
            pesquisa.Conteudo);

        contexto.Adicionar(
            "conhecimento:resposta",
            pesquisa.Conteudo);

        return true;
    }

    private static string ConstruirConsultaPesquisa(string entrada)
    {
        var texto = entrada.Trim();
        var normalizado = texto.ToLowerInvariant();

        if (normalizado.Contains(".net", StringComparison.Ordinal) ||
            normalizado.Contains("dotnet", StringComparison.Ordinal))
        {
            return $"site:learn.microsoft.com .NET C# programação linguagem: {texto}";
        }

        if (normalizado.Contains("c#", StringComparison.Ordinal) ||
            normalizado.Contains("csharp", StringComparison.Ordinal) ||
            normalizado.Contains("f#", StringComparison.Ordinal))
        {
            return $"linguagem de programação Microsoft .NET C# F# documentação oficial: {texto}";
        }

        return texto;
    }

    private static bool EhConhecimentoValido(string? conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            return false;

        var texto = conteudo.Trim();

        if (texto.StartsWith(
                "Pesquisa realizada para:",
                StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith(
                "Fontes recuperadas da pesquisa web para:",
                StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith(
                "Informações encontradas para:",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (texto.Length < 40)
            return false;

        var marcadoresDeFalha = new[]
        {
            "provedor de IA ainda não foi configurado",
            "chave Gemini não foi configurada",
            "falha na API Gemini",
            "não retornou texto utilizável",
            "não é possível responder",
            "não contém informações técnicas",
            "não contendo informações técnicas",
            "incerteza total sobre o tema",
            "erro ao pesquisar",
            "[KAYRON_DECISAO]",
            "\"proxima_acao\"",
            "\"parametros\""
        };

        return !marcadoresDeFalha.Any(marcador =>
            texto.Contains(marcador, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TemConhecimentoRelevante(IContexto contexto)
    {
        return !string.IsNullOrWhiteSpace(
            contexto.Obter("memoria:aprendida_relevante"));
    }

    private static string? ObterConhecimentoRelevante(IContexto contexto)
    {
        var chaves = contexto.Obter("memoria:aprendida_relevante");

        if (string.IsNullOrWhiteSpace(chaves))
            return null;

        var chave = chaves
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(chave)
            ? null
            : contexto.Obter($"memoria:aprendida:{chave}");
    }

    private static IContexto CriarContextoPesquisa(IContexto contexto)
    {
        contexto.Adicionar(
            "intencao_detectada",
            "internet:pesquisar");

        contexto.Adicionar(
            "continuacao_origem_internet",
            "nao");

        return contexto;
    }

    private static string CriarChave(string texto)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(texto.Trim().ToLowerInvariant()));

        return "conhecimento_" +
               Convert.ToHexString(bytes)[..24].ToLowerInvariant();
    }
}
