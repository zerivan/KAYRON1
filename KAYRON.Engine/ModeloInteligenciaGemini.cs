using System.Net.Http.Json;
using System.Text.Json;
using KAYRON.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KAYRON.Engine;

public sealed class ModeloInteligenciaGemini : IModeloInteligencia
{
    private readonly HttpClient _httpClient;
    private readonly ModeloInteligenciaOptions _options;
    private readonly ILogger<ModeloInteligenciaGemini> _logger;

    public ModeloInteligenciaGemini(
        HttpClient httpClient,
        IOptions<ModeloInteligenciaOptions> options,
        ILogger<ModeloInteligenciaGemini> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Resposta> GerarAsync(
        Instrucao instrucao,
        IContexto contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrucao);
        ArgumentNullException.ThrowIfNull(contexto);

        var apiKey = ObterApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "A chave Gemini não foi configurada. Defina GEMINI_API_KEY no ambiente.");
        }

        var modelo = string.IsNullOrWhiteSpace(_options.Modelo)
            ? "gemini-3.8-flash"
            : _options.Modelo.Trim();

        var prompt = ConstruirPrompt(instrucao, contexto);

        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = """
Você é a inteligência central da KAYRON.Engine. Sua função é atuar como um Orquestrador Factual de Estado e Higienizador de Contexto. Você recebe o Input do Usuário e um Bloco de Dados Brutos, que pode conter Memória Local e/ou Resultados de Busca do DeepSearch.

Para garantir a integridade do runtime do agente, aplique rigorosamente as seguintes 4 Leis de Processamento:

1. LEI DA CORRELAÇÃO DE ENTIDADE (Alinhamento de Escopo)

- Identifique o Núcleo Semântico, a Entidade Principal e o atributo ou informação solicitada no input do usuário.
- A resposta deve permanecer vinculada a esse núcleo durante todo o processamento.
- É terminantemente proibido substituir a entidade, o atributo, o período, o local ou o assunto solicitado por outro conteúdo apenas porque ele apareceu nos Dados Brutos.
- Se os Dados Brutos desviarem do núcleo da pergunta, trate esse conteúdo como ruído e não o utilize para redefinir a resposta.
- Uma palavra em comum entre a pergunta e uma fonte não é suficiente para estabelecer relevância.

2. LEI DA COERÊNCIA TEXTO-FONTE (Filtro de Desconexão Ambígua)

- Avalie se cada dado recuperado possui relação direta com o Núcleo Semântico identificado na pergunta.
- A fonte utilizada deve sustentar factual e diretamente a afirmação para a qual está sendo utilizada.
- Se o algoritmo de busca capturar homônimos, conceitos diferentes, entidades relacionadas, anúncios, holdings, páginas institucionais ou outros conteúdos que não respondam diretamente à pergunta, descarte esses dados como ruído.
- NUNCA utilize uma fonte apenas porque ela contém palavras-chave semelhantes às da pergunta.
- NUNCA liste na seção "Fontes consultadas" uma fonte que não sustente diretamente pelo menos uma afirmação apresentada na resposta.
- A confiabilidade do domínio não transforma uma fonte tematicamente desconexa em fonte relevante.

3. LEI DA REJEIÇÃO DE ALUCINAÇÃO CRUZADA

- Se os Dados Brutos forem insuficientes, corrompidos ou desconexos da pergunta, não tente combiná-los artificialmente para produzir uma resposta.
- Não complete lacunas das fontes com suposições.
- Não invente nomes, datas, números, cargos, entidades, URLs, títulos, fatos ou relações que não estejam sustentados pelos dados disponíveis.
- Quando o usuário tiver solicitado uma pesquisa explícita, não apresente conhecimento geral do modelo como se tivesse sido confirmado pela pesquisa.
- Se a pesquisa não fornecer evidência suficiente para responder com segurança, informe que os dados recuperados foram insuficientes ou inadequados.
- É preferível declarar insuficiência de evidência a produzir uma resposta aparentemente completa, porém desconectada das fontes recuperadas.

4. LEI DA SOBERANIA DO COMANDO DO USUÁRIO

- Quando o input contiver um comando explícito de pesquisa ou verificação, como "Pesquise", "Busque", "Procure", "Consulte", "Verifique" ou equivalente, preserve integralmente o objetivo factual solicitado pelo usuário.
- O comando do usuário define o assunto da pesquisa. Os resultados retornados pelos provedores são apenas dados candidatos e NÃO podem redefinir a pergunta.
- Ignore termos parasitários, metadados, anúncios, holdings, entidades relacionadas ou assuntos incidentais encontrados nos resultados quando eles não responderem ao núcleo da pergunta.
- A pesquisa deve permanecer orientada pela pergunta original desde a recuperação das fontes até a síntese final.
- Antes de finalizar uma resposta baseada em pesquisa, confirme internamente:
  1. A resposta trata exatamente da entidade e do atributo solicitados?
  2. Os dados utilizados realmente sustentam essa resposta?
  3. As fontes apresentadas correspondem diretamente ao conteúdo afirmado?
- Se qualquer uma dessas verificações falhar, rejeite o conteúdo desconexo antes de gerar a resposta final.

REGRA FINAL DE INTEGRIDADE:

Nunca permita que os Dados Brutos substituam ou redefinam a intenção original do usuário. Os Dados Brutos devem ser avaliados, filtrados e subordinados ao Núcleo Semântico da pergunta.

Quando a instrução começar com [KAYRON_DECISAO], ela é uma solicitação de planejamento: retorne SOMENTE o JSON solicitado, sem markdown e sem explicação.
""" }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = _options.Temperatura
            }
        };

        HttpResponseMessage? response = null;
        string body = string.Empty;

        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            response?.Dispose();

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent");

            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(payload);

            response = await _httpClient.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode ||
                ((int)response.StatusCode != 408 &&
                 (int)response.StatusCode != 429 &&
                 ((int)response.StatusCode < 500 || (int)response.StatusCode > 599)))
            {
                break;
            }

            if (tentativa < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, tentativa)), cancellationToken);
            }
        }
        using (response)

        if (!response!.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini retornou HTTP {Status}: {Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"Falha na API Gemini: HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var texto = ExtrairTexto(document);

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new InvalidOperationException(
                "A API Gemini não retornou texto utilizável.");
        }

        return new Resposta
        {
            Conteudo = texto.Trim()
        };
    }
    private string ObterApiKey()
    {
        return Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable(
                "GEMINI_API_KEY",
                EnvironmentVariableTarget.User)
            ?? _options.ApiKey;
    }

    private static string ConstruirPrompt(
        Instrucao instrucao,
        IContexto contexto)
    {
        var entrada = instrucao.Conteudo.StartsWith(
                "MODO: ANÁLISE DE PESQUISA DO KAYRON.",
                StringComparison.OrdinalIgnoreCase)
            ? instrucao.Conteudo
            : contexto.Obter("entrada_original") ?? instrucao.Conteudo;

        var contextoConversacional = contexto.Obter("contexto_conversacional");
        var memoria = contexto.Obter("memoria:aprendida_relevante");
        var valoresMemoria = string.IsNullOrWhiteSpace(memoria)
            ? Array.Empty<string>()
            : memoria
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(chave => contexto.Obter($"memoria:aprendida:{chave}"))
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Cast<string>()
                .ToArray();
        var valorMemoria = valoresMemoria.Length == 0
            ? null
            : string.Join(Environment.NewLine, valoresMemoria);
        var resultado = contexto.Obter("ciclo:ultimo_resultado");
        var conhecimentoAdquirido = contexto.Obter("conhecimento:adquirido");
        var intencao = contexto.Obter("intencao_detectada");
        var extraInstrucao = string.Empty;
        if (string.Equals(
                contexto.Obter("ciclo:resultado_disponivel"),
                "sim",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                contexto.Obter("ultima_origem_ferramenta"),
                "internet",
                StringComparison.OrdinalIgnoreCase))
        {
            extraInstrucao =
@"ATENCAO SOBRE EVIDENCIAS DA PESQUISA:
Use o conteudo de 'RESULTADO DA ULTIMA FERRAMENTA' como base factual principal e exclusiva para afirmacoes apresentadas como confirmadas pela pesquisa. Nao invente fatos, nomes, datas, numeros, titulos ou URLs ausentes dessa evidencia. Preserve as URLs e referencias fornecidas pelo resultado e apresente as fontes ao final da resposta. Associe cada fonte as afirmacoes que ela sustenta; nao atribua a uma fonte informacoes que ela nao contem. Se a evidencia for insuficiente, contraditoria ou nao responder exatamente a pergunta, declare essa limitacao em vez de preencher lacunas com suposicoes.";
        }

        return $"""
ENTRADA DO USUÁRIO:
{entrada}

INTENÇÃO DETECTADA:
{intencao ?? "não identificada"}

CONTEXTO DA CONVERSA:
{contextoConversacional ?? "não disponível"}

MEMÓRIA RELEVANTE:
{(string.IsNullOrWhiteSpace(valorMemoria) ? "nenhuma" : valorMemoria)}

RESULTADO DA ÚLTIMA FERRAMENTA:
{(string.IsNullOrWhiteSpace(resultado) ? "nenhum" : resultado)}

CONHECIMENTO ADQUIRIDO PELO KAYRON:
{(string.IsNullOrWhiteSpace(conhecimentoAdquirido) ? "nenhum" : conhecimentoAdquirido)}

{extraInstrucao}

TAREFA:
Responda diretamente ao usuário considerando os dados acima. Use o CONTEXTO DA CONVERSA exclusivamente para continuidade conversacional, referência ao que já foi dito e compreensão do encadeamento da conversa. Nunca utilize uma resposta anterior do KAYRON como evidência factual para responder à entrada atual. Para afirmações factuais, utilize somente a entrada atual, a MEMÓRIA RELEVANTE, o RESULTADO DA ÚLTIMA FERRAMENTA e o CONHECIMENTO ADQUIRIDO PELO KAYRON, conforme aplicável. Para perguntas informativas, entregue somente a resposta solicitada. Não acrescente frases sobre ações pendentes, ações concluídas, próximas ações ou estado interno do KAYRON, salvo se o usuário pedir esse tipo de informação. Não diga que executou uma ação se ela não aparece no resultado.
""";
    }

    private static string? ExtrairTexto(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("candidates", out var candidates))
            return null;

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts))
                continue;

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                    return text.GetString();
            }
        }

        return null;
    }
}