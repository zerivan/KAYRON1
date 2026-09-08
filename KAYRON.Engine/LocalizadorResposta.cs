using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class LocalizadorResposta : ILocalizadorResposta
{
    public string Localizar(
        string resposta,
        Idioma idioma,
        IContexto contexto)
    {
        ArgumentNullException.ThrowIfNull(idioma);
        ArgumentNullException.ThrowIfNull(contexto);

        if (string.IsNullOrWhiteSpace(resposta))
            return resposta;

        if (idioma.Codigo.Equals(
                "pt-BR",
                StringComparison.OrdinalIgnoreCase))
        {
            return resposta;
        }

        var texto = resposta.Trim();

        return idioma.Codigo.ToLowerInvariant() switch
        {
            "en-us" => Inglês(texto),
            "es-es" => Espanhol(texto),
            "fr-fr" => Francês(texto),
            "de-de" => Alemão(texto),
            "it-it" => Italiano(texto),
            _ => resposta
        };
    }

    private static string Inglês(string texto)
    {
        return Traduzir(
            texto,
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["KAYRON não recebeu uma mensagem."] =
                    "KAYRON did not receive a message.",

                ["KAYRON não recebeu nenhuma instrução."] =
                    "KAYRON did not receive an instruction.",

                ["KAYRON não recebeu conteúdo para analisar."] =
                    "KAYRON did not receive content to analyze.",

                ["KAYRON não conseguiu identificar a intenção."] =
                    "KAYRON could not identify the intention.",

                ["O Gemini não retornou conteúdo."] =
                    "Gemini did not return any content.",

                ["Ferramenta não encontrada:"] =
                    "Tool not found:",

                ["OPERAÇÃO BLOQUEADA:"] =
                    "OPERATION BLOCKED:",

                ["CONFIRMAÇÃO NECESSÁRIA:"] =
                    "CONFIRMATION REQUIRED:",

                ["Falha na execução do plano:"] =
                    "Plan execution failed:",

                ["Erro ao executar a ferramenta"] =
                    "Error executing tool"
            });
    }

    private static string Espanhol(string texto)
    {
        return Traduzir(
            texto,
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["KAYRON não recebeu uma mensagem."] =
                    "KAYRON no recibió un mensaje.",

                ["KAYRON não recebeu nenhuma instrução."] =
                    "KAYRON no recibió ninguna instrucción.",

                ["KAYRON não recebeu conteúdo para analisar."] =
                    "KAYRON no recibió contenido para analizar.",

                ["KAYRON não conseguiu identificar a intenção."] =
                    "KAYRON no pudo identificar la intención.",

                ["O Gemini não retornou conteúdo."] =
                    "Gemini no devolvió contenido.",

                ["Ferramenta não encontrada:"] =
                    "Herramienta no encontrada:",

                ["OPERAÇÃO BLOQUEADA:"] =
                    "OPERACIÓN BLOQUEADA:",

                ["CONFIRMAÇÃO NECESSÁRIA:"] =
                    "CONFIRMACIÓN NECESARIA:",

                ["Falha na execução do plano:"] =
                    "Error en la ejecución del plan:",

                ["Erro ao executar a ferramenta"] =
                    "Error al ejecutar la herramienta"
            });
    }

    private static string Francês(string texto)
    {
        return Traduzir(
            texto,
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["KAYRON não recebeu uma mensagem."] =
                    "KAYRON n’a reçu aucun message.",

                ["KAYRON não recebeu nenhuma instrução."] =
                    "KAYRON n’a reçu aucune instruction.",

                ["KAYRON não recebeu conteúdo para analisar."] =
                    "KAYRON n’a reçu aucun contenu à analyser.",

                ["KAYRON não conseguiu identificar a intenção."] =
                    "KAYRON n’a pas pu identifier l’intention.",

                ["O Gemini não retornou conteúdo."] =
                    "Gemini n’a renvoyé aucun contenu.",

                ["Ferramenta não encontrada:"] =
                    "Outil introuvable:",

                ["OPERAÇÃO BLOQUEADA:"] =
                    "OPÉRATION BLOQUÉE:",

                ["CONFIRMAÇÃO NECESSÁRIA:"] =
                    "CONFIRMATION NÉCESSAIRE:",

                ["Falha na execução do plano:"] =
                    "Échec de l’exécution du plan:",

                ["Erro ao executar a ferramenta"] =
                    "Erreur lors de l’exécution de l’outil"
            });
    }

    private static string Alemão(string texto)
    {
        return Traduzir(
            texto,
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["KAYRON não recebeu uma mensagem."] =
                    "KAYRON hat keine Nachricht erhalten.",

                ["KAYRON não recebeu nenhuma instrução."] =
                    "KAYRON hat keine Anweisung erhalten.",

                ["KAYRON não recebeu conteúdo para analisar."] =
                    "KAYRON hat keinen Inhalt zur Analyse erhalten.",

                ["KAYRON não conseguiu identificar a intenção."] =
                    "KAYRON konnte die Absicht nicht erkennen.",

                ["O Gemini não retornou conteúdo."] =
                    "Gemini hat keinen Inhalt zurückgegeben.",

                ["Ferramenta não encontrada:"] =
                    "Werkzeug nicht gefunden:",

                ["OPERAÇÃO BLOQUEADA:"] =
                    "VORGANG BLOCKIERT:",

                ["CONFIRMAÇÃO NECESSÁRIA:"] =
                    "BESTÄTIGUNG ERFORDERLICH:",

                ["Falha na execução do plano:"] =
                    "Fehler bei der Planausführung:",

                ["Erro ao executar a ferramenta"] =
                    "Fehler bei der Ausführung des Werkzeugs"
            });
    }

    private static string Italiano(string texto)
    {
        return Traduzir(
            texto,
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["KAYRON não recebeu uma mensagem."] =
                    "KAYRON non ha ricevuto alcun messaggio.",

                ["KAYRON não recebeu nenhuma instrução."] =
                    "KAYRON non ha ricevuto alcuna istruzione.",

                ["KAYRON não recebeu conteúdo para analisar."] =
                    "KAYRON non ha ricevuto contenuti da analizzare.",

                ["KAYRON não conseguiu identificar a intenção."] =
                    "KAYRON non è riuscito a identificare l'intenzione.",

                ["O Gemini não retornou conteúdo."] =
                    "Gemini non ha restituito alcun contenuto.",

                ["Ferramenta não encontrada:"] =
                    "Strumento non trovato:",

                ["OPERAÇÃO BLOQUEADA:"] =
                    "OPERAZIONE BLOCCATA:",

                ["CONFIRMAÇÃO NECESSÁRIA:"] =
                    "CONFERMA NECESSARIA:",

                ["Falha na execução do plano:"] =
                    "Errore nell'esecuzione del piano:",

                ["Erro ao executar a ferramenta"] =
                    "Errore durante l'esecuzione dello strumento"
            });
    }

    private static string Traduzir(
        string texto,
        IReadOnlyDictionary<string, string> traducoes)
    {
        var resultado = texto;

        foreach (var traducao in traducoes)
        {
            resultado = resultado.Replace(
                traducao.Key,
                traducao.Value,
                StringComparison.OrdinalIgnoreCase);
        }

        return resultado;
    }
}
