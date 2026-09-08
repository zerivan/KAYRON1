namespace KAYRON.Core;

public interface IGerenciadorIdioma
{
    Idioma Atual { get; }

    void Definir(
        Idioma idioma);

    Idioma Obter(
        string codigo);
}
