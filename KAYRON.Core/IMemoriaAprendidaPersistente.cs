namespace KAYRON.Core;

public interface IMemoriaAprendidaPersistente
{
    void Salvar(MemoriaAprendida memoria);
    bool Remover(string chave);
    MemoriaAprendida? Recuperar(string chave);
    IReadOnlyCollection<MemoriaAprendida> Listar();
}
