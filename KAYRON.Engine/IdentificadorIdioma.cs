using KAYRON.Core;
using System.Globalization;
using System.Text;

namespace KAYRON.Engine;

public sealed class IdentificadorIdioma
    : IIdentificadorIdioma
{
    private static readonly Dictionary<string, string[]> Vocabulos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pt-BR"] =
            [
                "que", "qual", "quais", "como", "onde",
                "mostre", "mostrar", "liste", "listar",
                "arquivo", "arquivos", "sistema", "memoria",
                "processo", "processos", "programa",
                "ajude", "ajuda", "olá", "ola",
                "você", "voce", "para", "com", "dos",
                "das", "uma", "um", "isso", "este",
                "esse", "recupere", "recuperar"
            ],

            ["en-US"] =
            [
                "what", "which", "how", "where", "show",
                "list", "file", "files", "system", "memory",
                "process", "processes", "program",
                "help", "hello", "you", "this", "that",
                "retrieve", "get", "from", "with"
            ],

            ["es-ES"] =
            [
                "qué", "que", "cuál", "cuáles", "cómo",
                "dónde", "muestra", "mostrar", "lista",
                "listar", "archivo", "archivos", "sistema",
                "memoria", "proceso", "procesos", "programa",
                "ayuda", "hola", "este", "ese", "recupera"
            ],

            ["fr-FR"] =
            [
                "quoi", "quel", "quels", "comment", "où",
                "montre", "montrer", "liste", "lister",
                "fichier", "fichiers", "système", "mémoire",
                "processus", "programme", "aide", "bonjour",
                "ce", "cet", "cette", "récupérer"
            ],

            ["de-DE"] =
            [
                "was", "welche", "wie", "wo", "zeige",
                "zeigen", "liste", "listen", "datei",
                "dateien", "system", "speicher",
                "prozess", "prozesse", "programm",
                "hilfe", "hallo", "diese", "dieser"
            ],

            ["it-IT"] =
            [
                "cosa", "quale", "quali", "come", "dove",
                "mostra", "mostrare", "elenca", "elencare",
                "file", "sistema", "memoria", "processo",
                "processi", "programma", "aiuto", "ciao",
                "questo", "questa", "recupera"
            ]
        };

    public ResultadoIdioma Identificar(
        string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new ResultadoIdioma
            {
                Idioma = ObterIdioma("pt-BR"),
                Confianca = 0,
                Identificado = false
            };
        }

        var tokens =
            Tokenizar(texto);

        var pontuacoes =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var idioma in Vocabulos)
        {
            var pontuacao = 0;

            foreach (var token in tokens)
            {
                if (idioma.Value.Any(
                        palavra =>
                            string.Equals(
                                Normalizar(token),
                                Normalizar(palavra),
                                StringComparison.OrdinalIgnoreCase)))
                {
                    pontuacao++;
                }
            }

            pontuacoes[idioma.Key] =
                pontuacao;
        }

        var melhor =
            pontuacoes
                .OrderByDescending(
                    item => item.Value)
                .First();

        var total =
            tokens.Length;

        var confianca =
            total == 0
                ? 0
                : (double)melhor.Value / total;

        return new ResultadoIdioma
        {
            Idioma =
                ObterIdioma(melhor.Key),

            Confianca =
                Math.Min(
                    confianca * 2.0,
                    1.0),

            Identificado =
                melhor.Value > 0
        };
    }

    private static string[] Tokenizar(
        string texto)
    {
        return texto.Split(
            [
                ' ', '\t', '\r', '\n',
                ',', '.', ';', ':',
                '!', '?', '(', ')',
                '[', ']'
            ],
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static string Normalizar(
        string texto)
    {
        var normalizado =
            texto.Normalize(
                NormalizationForm.FormD);

        var caracteres =
            normalizado
                .Where(
                    caractere =>
                        CharUnicodeInfo.GetUnicodeCategory(
                            caractere) !=
                        UnicodeCategory.NonSpacingMark)
                .ToArray();

        return new string(caracteres)
            .Normalize(
                NormalizationForm.FormC)
            .ToLowerInvariant();
    }

    private static Idioma ObterIdioma(
        string codigo)
    {
        return codigo switch
        {
            "en-US" =>
                new Idioma
                {
                    Codigo = "en-US",
                    Nome = "English",
                    Regiao = "United States"
                },

            "es-ES" =>
                new Idioma
                {
                    Codigo = "es-ES",
                    Nome = "Español",
                    Regiao = "España"
                },

            "fr-FR" =>
                new Idioma
                {
                    Codigo = "fr-FR",
                    Nome = "Français",
                    Regiao = "France"
                },

            "de-DE" =>
                new Idioma
                {
                    Codigo = "de-DE",
                    Nome = "Deutsch",
                    Regiao = "Deutschland"
                },

            "it-IT" =>
                new Idioma
                {
                    Codigo = "it-IT",
                    Nome = "Italiano",
                    Regiao = "Italia"
                },

            _ =>
                new Idioma
                {
                    Codigo = "pt-BR",
                    Nome = "Português",
                    Regiao = "Brasil"
                }
        };
    }
}

