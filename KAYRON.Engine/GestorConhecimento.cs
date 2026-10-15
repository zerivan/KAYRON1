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

        var intencaoDetectada =
            contexto.Obter("intencao_detectada") ?? string.Empty;

        var pesquisaSolicitada = string.Equals(
            intencaoDetectada,
            "internet:pesquisar",
            StringComparison.OrdinalIgnoreCase);

        if (intencaoDetectada.StartsWith(
                "conversa:",
                StringComparison.OrdinalIgnoreCase))
        {
            contexto.Adicionar(
                "conhecimento:estado",
                "nao_solicitado");

            return false;
        }

        if (!pesquisaSolicitada && !DeveAdquirirConhecimento(entrada))
        {
            contexto.Adicionar(
                "conhecimento:estado",
                "nao_solicitado");

            return false;
        }

        if (!pesquisaSolicitada)
        {
            var memoriaExistente = _memoriaAprendida
                .Pesquisar(entrada, 3)
                .Where(EhMemoriaUtilizavel)
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

    private static bool DeveAdquirirConhecimento(string entrada)
    {
        var texto = entrada.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(texto))
            return false;

        if (texto is "ola" or "olá" or "oi" or "bom dia" or "boa tarde" or "boa noite" or
            "tudo bem" or "como voce esta" or "como você está")
            return false;

        var pareceMatematica =
            texto.Contains("ao quadrado", StringComparison.Ordinal) ||
            texto.Contains("elevado a", StringComparison.Ordinal) ||
            texto.Contains("potencia de", StringComparison.Ordinal) ||
            texto.Contains("potenciacao", StringComparison.Ordinal) ||
            texto.Contains("potenciação", StringComparison.Ordinal) ||
            texto.Any(char.IsDigit) &&
            texto.Any(caractere => "+-*/=×÷".Contains(caractere));

        if (pareceMatematica)
            return false;

        return texto.Contains("qual ", StringComparison.Ordinal) ||
               texto.StartsWith("qual?", StringComparison.Ordinal) ||
               texto.Contains("quais ", StringComparison.Ordinal) ||
               texto.Contains("quem ", StringComparison.Ordinal) ||
               texto.Contains("o que ", StringComparison.Ordinal) ||
               texto.Contains("como ", StringComparison.Ordinal) ||
               texto.Contains("quando ", StringComparison.Ordinal) ||
               texto.Contains("onde ", StringComparison.Ordinal) ||
               texto.Contains("por que ", StringComparison.Ordinal) ||
               texto.Contains("porque ", StringComparison.Ordinal) ||
               texto.Contains("quanto ", StringComparison.Ordinal) ||
               texto.Contains("quantos ", StringComparison.Ordinal) ||
               texto.Contains("quantas ", StringComparison.Ordinal) ||
               texto.Contains("explique ", StringComparison.Ordinal) ||
               texto.Contains("significa ", StringComparison.Ordinal) ||
               texto.Contains("defina ", StringComparison.Ordinal) ||
               texto.Contains("principais ", StringComparison.Ordinal) ||
               texto.Contains("diferença entre ", StringComparison.Ordinal) ||
               texto.Contains("história de ", StringComparison.Ordinal);
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

    private static bool EhMemoriaUtilizavel(KAYRON.Core.MemoriaAprendida memoria)
    {
        if (!string.Equals(memoria.Tipo, "conhecimento", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(memoria.Origem, "pesquisa_verificada", StringComparison.OrdinalIgnoreCase) ||
            !memoria.Tags.Any(tag => string.Equals(tag, "verificado", StringComparison.OrdinalIgnoreCase)))
            return false;

        var referencia = memoria.AtualizadoEm > memoria.AprendidoEm
            ? memoria.AtualizadoEm
            : memoria.AprendidoEm;

        return DateTime.UtcNow - referencia <= TimeSpan.FromDays(7);
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
