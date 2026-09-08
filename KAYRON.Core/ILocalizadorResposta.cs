namespace KAYRON.Core;

public interface ILocalizadorResposta
{
    string Localizar(
        string resposta,
        Idioma idioma,
        IContexto contexto);
}
