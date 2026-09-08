namespace KAYRON.Core;

public interface IDetectorProjeto
{
    string? DetectarRaiz(string diretorio);

    bool EhProjeto(string diretorio);

    string? ObterArquivoProjeto(string diretorio);
}
