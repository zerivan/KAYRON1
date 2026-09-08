namespace KAYRON.Core;

public interface IMemoriaAprendida
{
    void Aprender(string chave, string valor);
    MemoriaAprendida? Recuperar(string chave);
    IReadOnlyCollection<MemoriaAprendida> Listar();
}
