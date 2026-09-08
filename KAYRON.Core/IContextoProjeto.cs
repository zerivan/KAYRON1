namespace KAYRON.Core;

public interface IContextoProjeto
{
    InformacoesProjeto? Atual { get; }

    InformacoesProjeto? Detectar(
        string diretorio);

    void Definir(
        InformacoesProjeto projeto);
}
