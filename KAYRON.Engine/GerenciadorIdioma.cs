using KAYRON.Core;

namespace KAYRON.Engine;

public sealed class GerenciadorIdioma
    : IGerenciadorIdioma
{
    private readonly Dictionary<string, Idioma> _idiomas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pt-BR"] = new Idioma
            {
                Codigo = "pt-BR",
                Nome = "Português",
                Regiao = "Brasil"
            },

            ["en-US"] = new Idioma
            {
                Codigo = "en-US",
                Nome = "English",
                Regiao = "United States"
            },

            ["es-ES"] = new Idioma
            {
                Codigo = "es-ES",
                Nome = "Español",
                Regiao = "España"
            },

            ["fr-FR"] = new Idioma
            {
                Codigo = "fr-FR",
                Nome = "Français",
                Regiao = "France"
            },

            ["de-DE"] = new Idioma
            {
                Codigo = "de-DE",
                Nome = "Deutsch",
                Regiao = "Deutschland"
            },

            ["it-IT"] = new Idioma
            {
                Codigo = "it-IT",
                Nome = "Italiano",
                Regiao = "Italia"
            }
        };

    private Idioma _atual =
        new()
        {
            Codigo = "pt-BR",
            Nome = "Português",
            Regiao = "Brasil"
        };

    public Idioma Atual =>
        _atual;

    public void Definir(
        Idioma idioma)
    {
        ArgumentNullException.ThrowIfNull(
            idioma);

        if (!_idiomas.TryGetValue(
                idioma.Codigo,
                out var idiomaRegistrado))
        {
            throw new ArgumentException(
                $"Idioma não suportado: {idioma.Codigo}",
                nameof(idioma));
        }

        _atual =
            idiomaRegistrado;
    }

    public Idioma Obter(
        string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return _atual;
        }

        return _idiomas.TryGetValue(
                codigo.Trim(),
                out var idioma)
            ? idioma
            : _atual;
    }
}
