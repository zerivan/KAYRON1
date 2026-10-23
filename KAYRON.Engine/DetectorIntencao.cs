using System.Text.RegularExpressions;
using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class DetectorIntencao : IDetectorIntencao
{
    public IntencaoDetectada Detectar(
        string texto,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var normalizado = Normalizar(texto);

        // A intenção explícita do turno atual tem prioridade sobre qualquer
        // contexto ou intenção inferida anteriormente.
        if (ContemAlgum(normalizado,
                "pesquise",
                "pesquisar",
                "pesquisa",
                "pesquise novamente",
                "pesquisar novamente",
                "nova pesquisa",
                "faca uma pesquisa",
                "faca nova pesquisa",
                "quero pesquisar",
                "quero que pesquise",
                "pode pesquisar",
                "procure na internet",
                "procure na web",
                "procure novamente",
                "busque na internet",
                "busque na web",
                "busque novamente",
                "consulte na internet",
                "consulte na web",
                "pesquise na internet",
                "pesquise na web",
                "qual e o atual",
                "quem e o atual",
                "preco atual",
                "cotacao atual",
                "cotacao do dolar",
                "cotacao dolar",
                "valor do dolar",
                "dolar hoje",
                "dolar agora",
                "preco do dolar",
                "noticias de hoje"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "internet",
                Operacao = "pesquisar",
                Confianca = 1.0,
                Identificada = true,
                Evidencia = texto
            };
        }

        // Se o turno atual é uma continuação explícita de uma pesquisa,
        // preserva a intenção de pesquisa sem reutilizar a resposta anterior
        // como se ela fosse a nova intenção.
        if (string.Equals(
                contexto.Obter("continuacao_origem_internet"),
                "sim",
                StringComparison.OrdinalIgnoreCase) &&
            ContemAlgum(normalizado,
                "sobre isso",
                "sobre isto",
                "sobre esse assunto",
                "sobre este assunto",
                "a respeito disso",
                "a respeito disto"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "internet",
                Operacao = "pesquisar",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (ContemAlgum(normalizado,
                "corrija os erros",
                "corrigir os erros",
                "corrija erro",
                "corrigir erro",
                "corrija o projeto",
                "corrigir o projeto",
                "corrija este projeto",
                "corrigir este projeto",
                "encontre e corrija",
                "encontrar e corrigir",
                "autocorrija",
                "autocorrigir",
                "auto corrija",
                "auto corrigir"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "autocorrecao",
                Operacao = "autocorrigir",
                Confianca = 0.98,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (EhComentarioSobrePesquisa(normalizado))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "conversa",
                Operacao = "responder",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (EhSolicitacaoDeFonte(normalizado))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "internet",
                Operacao = "pesquisar",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = "solicitacao_de_fonte"
            };
        }

        if (EhPerguntaFactualQueExigePesquisa(normalizado))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "internet",
                Operacao = "pesquisar",
                Confianca = 0.90,
                Identificada = true,
                Evidencia = "pergunta_factual_exige_verificacao_externa"
            };
        }

        if (ContemAlgum(normalizado,
                "pesquise",
                "quero pesquisar",
                "preciso pesquisar",
                "pesquisa sobre",
                "pesquisa na internet",
                "pesquisa na web",
                "pesquisar sobre",
                "pesquisar na internet",
                "pesquisar na web",
                "faÃ§a uma pesquisa",
                "faca uma pesquisa",
                "procure na internet",
                "procure na web",
                "pesquise na internet",
                "pesquise na web",
                "busque na internet",
                "busque na web",
                "qual e o atual",
                "quem e o atual",
                "preco atual",
                "cotacao atual",
                "cotacao do dolar",
                "cotacao dolar",
                "valor do dolar",
                "dolar hoje",
                "dolar agora",
                "preco do dolar",
                "noticias de hoje",
                "modelo mais recente",
                "modelos mais recentes",
                "versao mais recente",
                "versao atual",
                "versao mais nova",
                "ultima versao",
                "Ãºltima versÃ£o",
                "modelo atual",
                "modelo novo",
                "modelos novos",
                "quando foi lancado",
                "quando foi lanÃ§ado",
                "data de lancamento",
                "data de lanÃ§amento",
                "lancamento mais recente",
                "lanÃ§amento mais recente",
                "novidades sobre",
                "atualizacao mais recente",
                "atualizaÃ§Ã£o mais recente",
                "mudancas recentes",
                "mudanÃ§as recentes",
                "release mais recente",
                "atualizado recentemente"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "internet",
                Operacao = "pesquisar",
                Confianca = 0.97,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (ContemAlgum(normalizado,
                "analise o codigo",
                "analisar o codigo",
                "analise este codigo",
                "analisar este codigo",
                "encontre o erro",
                "encontrar o erro",
                "descubra o erro",
                "diagnostique o erro"))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "analise-codigo",
                Operacao = "analisar",
                Confianca = 0.95,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (EhPedidoExplicacao(normalizado))
        {
            var expressaoExplicada = ExtrairExpressaoParaExplicacao(normalizado);

            if (EhMatematica(expressaoExplicada))
            {
                return new IntencaoDetectada
                {
                    Ferramenta = "matematica",
                    Operacao = "explicar",
                    Confianca = 0.99,
                    Identificada = true,
                    Evidencia = expressaoExplicada
                };
            }
        }

        if (EhConversao(normalizado))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "conversao",
                Operacao = "converter",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = texto
            };
        }

        if (EhMatematica(normalizado))
        {
            return new IntencaoDetectada
            {
                Ferramenta = "matematica",
                Operacao = "calcular",
                Confianca = 0.99,
                Identificada = true,
                Evidencia = texto
            };
        }

        return new IntencaoDetectada
        {
            Ferramenta = string.Empty,
            Operacao = string.Empty,
            Confianca = 0,
            Identificada = false,
            Evidencia = texto
        };
    }

    private static bool EhPedidoExplicacao(string texto)
    {
        return ContemAlgum(
            texto,
            "mostre o passo a passo",
            "mostrar o passo a passo",
            "passo a passo",
            "como chegou ao resultado",
            "como chegou",
            "como foi calculado",
            "explique",
            "explica",
            "explicar");
    }

    private static string ExtrairExpressaoParaExplicacao(string texto)
    {
        var marcadores = new[]
        {
            "mostre o passo a passo",
            "mostrar o passo a passo",
            "passo a passo",
            "como chegou ao resultado",
            "como chegou",
            "como foi calculado",
            "explique",
            "explica",
            "explicar"
        };

        foreach (var marcador in marcadores)
        {
            if (!texto.StartsWith(marcador, StringComparison.OrdinalIgnoreCase))
                continue;

            var expressao = texto[marcador.Length..].Trim();

            expressao = Regex.Replace(
                expressao,
                @"^(?:de|da|do|a|o)\s+",
                string.Empty,
                RegexOptions.CultureInvariant);

            return expressao.Trim();
        }

        return texto.Trim();
    }

    private static bool EhConversao(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        const string unidade =
            @"(?:km|quilometro|quilometros|m|metro|metros|cm|centimetro|centimetros|mm|milimetro|milimetros|kg|quilograma|quilogramas|g|grama|gramas|mg|miligrama|miligramas|h|hora|horas|min|minuto|minutos|s|segundo|segundos|°?c|celsius|°?f|fahrenheit|k|kelvin)";

        const string unidadeComposta =
            $@"{unidade}(?:/{unidade})?";

        return Regex.IsMatch(
            texto,
            $@"^\s*(?:converter|converta\s+)?\s*\d+(?:[.,]\d+)?\s*{unidadeComposta}\s+(?:em|para)\s+{unidadeComposta}\s*$",
            RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                texto,
                $@"^\s*quanto\s+(?:da|dÃ¡)\s+\d+(?:[.,]\d+)?\s*{unidadeComposta}\s+(?:em|para)\s+{unidadeComposta}\s*$",
                RegexOptions.CultureInvariant);
    }

    private static bool EhMatematica(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var expressao = Regex.IsMatch(
            texto,
            @"^\s*(?=.*\d)(?=.*[+\-*/x×÷%])[\d\s.,()+\-*/x×÷%]+\s*$",
            RegexOptions.CultureInvariant);

        if (expressao)
        {
            var nivelParenteses = 0;

            foreach (var caractere in texto)
            {
                if (caractere == '(')
                {
                    nivelParenteses++;
                }
                else if (caractere == ')')
                {
                    nivelParenteses--;

                    if (nivelParenteses < 0)
                        return false;
                }
            }

            if (nivelParenteses == 0)
                return true;
        }

        var formulaComVariaveis = Regex.IsMatch(
            texto,
            @"^\s*(?:(?:[A-Za-z]\s*=\s*[+-]?\d+(?:[.,]\d+)?\s*,\s*)*)(?:[A-Za-z]\s*=\s*)?[A-Za-z](?:\s*[+\-*/x×÷%]\s*(?:[A-Za-z]|\d+(?:[.,]\d+)?))+\s*$",
            RegexOptions.CultureInvariant);

        return formulaComVariaveis;
    }

    private static bool EhPerguntaFactualQueExigePesquisa(
        string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        if (ContemAlgum(
                texto,
                "quem e voce",
                "quem e vc",
                "quem sou eu",
                "quem somos nos",
                "o que voce acha",
                "o que voce pensa"))
        {
            return false;
        }

        if (Regex.IsMatch(
                texto,
                @"^\s*(?:quem\s+(?:e|foi|sao|foram|ganhou|venceu|ocupa|governa)\b|onde\s+(?:fica|ficam|esta|estao|se localiza|se localizam)\b|quando\s+(?:nasceu|morreu|ocorreu|aconteceu|foi criado|foi fundada|foi fundado|foi lancado|foi lancada)\b|quant(?:os|as)\b)",
                RegexOptions.CultureInvariant))
        {
            return true;
        }

        return ContemAlgum(
            texto,
            "o que e ",
            "defina ",
            "definicao de ",
            "significado de ",
            "exemplos de ",
            "qual e o presidente",
            "qual e a presidente",
            "quem e o presidente",
            "quem e a presidente",
            "presidente atual do brasil",
            "presidente do brasil",
            "governador do maranhao",
            "prefeito de ",
            "quem sao os deputados",
            "quais sao os deputados",
            "deputados eleitos",
            "deputados federais eleitos",
            "deputados estaduais eleitos",
            "resultado das eleicoes",
            "resultados das eleicoes",
            "resultado da eleicao",
            "mais votado",
            "mais votados",
            "quantos votos",
            "qual a populacao",
            "qual e a populacao",
            "populacao atual",
            "qual e a capital",
            "qual a capital",
            "qual foi o resultado",
            "quais foram os resultados",
            "quanto custa",
            "quanto esta custando",
            "preco hoje",
            "preco atual");
    }

    private static bool EhSolicitacaoDeFonte(
        string texto)
    {
        return ContemAlgum(
            texto,
            "qual a fonte",
            "qual e a fonte",
            "a fonte desta informacao",
            "a fonte dessa informacao",
            "a fonte desta informaÃ§Ã£o",
            "a fonte dessa informaÃ§Ã£o",
            "fonte desta informacao",
            "fonte dessa informacao",
            "fonte desta informaÃ§Ã£o",
            "fonte dessa informaÃ§Ã£o",
            "qual a origem desta informacao",
            "qual a origem dessa informacao",
            "qual a origem desta informaÃ§Ã£o",
            "qual a origem dessa informaÃ§Ã£o",
            "de onde veio essa informacao",
            "de onde veio essa informaÃ§Ã£o");
    }

    private static bool EhComentarioSobrePesquisa(
        string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        return Regex.IsMatch(
            texto,
            @"^(que bom\s+)?(voce|tu)\s+(pesquisa|pesquisas|pesquisou|vai pesquisar|pode pesquisar|consegue pesquisar)\b",
            RegexOptions.CultureInvariant);
    }

    private static bool ContemAlgum(
        string texto,
        params string[] termos)
    {
        foreach (var termo in termos)
        {
            if (Regex.IsMatch(
                    texto,
                    $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(termo)}(?![\p{{L}}\p{{N}}])",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalizar(
        string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var normalizado = texto.Trim().ToLowerInvariant();

        normalizado = normalizado
            .Replace('\u00E1', 'a')
            .Replace('\u00E0', 'a')
            .Replace('\u00E3', 'a')
            .Replace('\u00E2', 'a')
            .Replace('\u00E4', 'a')
            .Replace('\u00E9', 'e')
            .Replace('\u00E8', 'e')
            .Replace('\u00EA', 'e')
            .Replace('\u00EB', 'e')
            .Replace('\u00ED', 'i')
            .Replace('\u00EC', 'i')
            .Replace('\u00EE', 'i')
            .Replace('\u00EF', 'i')
            .Replace('\u00F3', 'o')
            .Replace('\u00F2', 'o')
            .Replace('\u00F5', 'o')
            .Replace('\u00F4', 'o')
            .Replace('\u00F6', 'o')
            .Replace('\u00FA', 'u')
            .Replace('\u00F9', 'u')
            .Replace('\u00FB', 'u')
            .Replace('\u00FC', 'u')
            .Replace('\u00E7', 'c');
        return normalizado;
    }
}
